using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopPet.Models;
// WPF 專案（UseWPF）的隱式 using 會帶入 System.Windows.Shapes.Path，與 System.IO.Path 撞名；
// 用別名把 Path 固定為 System.IO.Path，避免 CS0104 歧義（勿移除）。
using Path = System.IO.Path;

namespace DesktopPet.Utils;

/// <summary>
/// 存檔讀寫與序列化中樞（設計檔 §4 / §5.2 / §8）。
///
/// 職責：
/// <list type="bullet">
///   <item>集中設定 <see cref="JsonSerializerOptions"/>：屬性 camelCase、列舉以字串序列化
///     （<c>PetMood.LowEnergy → "LOW_ENERGY"</c>，見 §4 已知限制）。</item>
///   <item>依 §8.1 將 <see cref="GameState"/> 切分為 <c>pet_data.json</c> / <c>settings.json</c>
///     / <c>achievements.json</c> 三檔讀寫。</item>
///   <item>寫入採「暫存檔 + 原子替換」，並保留最後 3 份備份（§8.2）；讀取失敗時自備份鏈復原。</item>
///   <item>提供 5 分鐘自動保存掛點（§8.2），由上層（E4）接上狀態來源。</item>
/// </list>
/// </summary>
/// <remarks>
/// §5.2 的存檔範例把 <c>pets</c> 與 <c>settings</c> 併於一份 JSON；此處遵循 §8.1 的「文件位置」
/// 拆為多檔（設定可獨立於寵物 tick 保存）。<c>maxPetSlots</c> 隨 <c>pets</c> 存於 <c>pet_data.json</c>。
/// </remarks>
public sealed class StorageManager : IDisposable
{
    /// <summary>自動保存間隔（§8.2：每 5 分鐘）。</summary>
    public static readonly TimeSpan AutoSaveInterval = TimeSpan.FromMinutes(5);

    /// <summary>保留的備份份數（§8.2：最後 3 個版本）。</summary>
    private const int BackupCount = 3;

    private const string PetDataFileName = "pet_data.json";
    private const string SettingsFileName = "settings.json";
    private const string AchievementsFileName = "achievements.json";

    /// <summary>存檔根目錄。預設 <c>%APPDATA%\DesktopPet\</c>（§8.1），可於建構時覆寫以利測試。</summary>
    public string DataDirectory { get; }

    private string PetDataPath => Path.Combine(DataDirectory, PetDataFileName);
    private string SettingsPath => Path.Combine(DataDirectory, SettingsFileName);
    private string AchievementsPath => Path.Combine(DataDirectory, AchievementsFileName);

    // 共用且已設定好的序列化選項；首次使用後即凍結（.NET 8），對外唯讀共用。
    private static readonly JsonSerializerOptions _jsonOptions = CreateJsonOptions();

    /// <summary>供其他元件（如 skins.json / pet_visuals.json 讀取）沿用的共用序列化選項。</summary>
    public static JsonSerializerOptions JsonOptions => _jsonOptions;

    private readonly object _ioLock = new();
    private System.Timers.Timer? _autoSaveTimer;
    private Func<GameState>? _stateProvider;

    public StorageManager(string? dataDirectory = null)
    {
        DataDirectory = dataDirectory
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DesktopPet");
        Directory.CreateDirectory(DataDirectory);
    }

    /// <summary>
    /// 建立共用序列化選項。列舉以 <see cref="JsonNamingPolicy.SnakeCaseUpper"/> 轉為大寫蛇形
    /// （<c>LowEnergy → "LOW_ENERGY"</c>、<c>Neutral → "NEUTRAL"</c>），讀寫皆套用同一對應，
    /// 確保舊存檔可正確讀回（§4）。
    /// </summary>
    private static JsonSerializerOptions CreateJsonOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,   // 讀取時容忍大小寫差異，增加向後相容
        WriteIndented = true,                 // 存檔可讀，便於除錯
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper),
        },
        // 不忽略 null：sounds.clickSoundPath = null 具語意（使用預設音效，§5.2）。
    };

    // ── 讀取 ─────────────────────────────────────────────────────

    /// <summary>
    /// 載入存檔並組成 <see cref="GameState"/>。任一檔案缺漏即採該部分的預設值
    /// （首次啟動時回傳全新的預設 <see cref="GameState"/>）。
    /// </summary>
    public GameState Load()
    {
        lock (_ioLock)
        {
            var state = new GameState();

            var petData = ReadJsonWithRecovery<PetDataFile>(PetDataPath);
            if (petData is not null)
            {
                state.Pets = petData.Pets;
                state.MaxPetSlots = petData.MaxPetSlots;
            }

            var settings = ReadJsonWithRecovery<Settings>(SettingsPath);
            if (settings is not null)
            {
                state.Settings = settings;
            }

            var achievements = ReadJsonWithRecovery<Dictionary<string, int>>(AchievementsPath);
            if (achievements is not null)
            {
                state.Achievements = achievements;
            }

            return state;
        }
    }

    // ── 寫入 ─────────────────────────────────────────────────────

    /// <summary>
    /// 將 <see cref="GameState"/> 寫入三份存檔。每檔各自原子替換並輪替備份。
    /// 寫入失敗（磁碟滿、權限不足等）會向上拋出，由呼叫端決定如何處理。
    /// </summary>
    public void Save(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        lock (_ioLock)
        {
            WriteJson(PetDataPath, new PetDataFile
            {
                Pets = state.Pets,
                MaxPetSlots = state.MaxPetSlots,
            });
            WriteJson(SettingsPath, state.Settings);
            WriteJson(AchievementsPath, state.Achievements);
        }
    }

    // ── 自動保存掛點（§8.2）────────────────────────────────────────

    /// <summary>
    /// 啟動每 5 分鐘的自動保存。<paramref name="stateProvider"/> 於每次觸發時提供當下狀態快照。
    /// 觸發在背景執行緒，呼叫端（E4）需確保提供的是一致快照或自行 marshal 到 UI 執行緒。
    /// </summary>
    public void StartAutoSave(Func<GameState> stateProvider)
    {
        ArgumentNullException.ThrowIfNull(stateProvider);
        _stateProvider = stateProvider;

        StopAutoSave();
        _autoSaveTimer = new System.Timers.Timer(AutoSaveInterval.TotalMilliseconds)
        {
            AutoReset = true,
        };
        _autoSaveTimer.Elapsed += OnAutoSaveElapsed;
        _autoSaveTimer.Start();
    }

    /// <summary>停止自動保存。可安全重複呼叫。</summary>
    public void StopAutoSave()
    {
        if (_autoSaveTimer is null)
        {
            return;
        }

        _autoSaveTimer.Stop();
        _autoSaveTimer.Elapsed -= OnAutoSaveElapsed;
        _autoSaveTimer.Dispose();
        _autoSaveTimer = null;
    }

    private void OnAutoSaveElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        // 自動保存的例外不可讓計時器執行緒崩潰：本次略過，等下一輪或關閉前保存。
        try
        {
            var state = _stateProvider?.Invoke();
            if (state is not null)
            {
                Save(state);
            }
        }
        catch
        {
            // 靜默略過本次自動保存。
        }
    }

    public void Dispose() => StopAutoSave();

    // ── 內部：JSON 讀寫 + 備份輪替 ─────────────────────────────────

    /// <summary>
    /// 依序嘗試主檔與 <c>.bak1</c>…<c>.bak3</c>，回傳第一個可成功反序列化者；全部失敗回傳
    /// <c>default</c>（由上層採預設值）。存檔損毀不讓程式崩潰。
    /// </summary>
    private static T? ReadJsonWithRecovery<T>(string path)
    {
        foreach (var candidate in EnumerateReadCandidates(path))
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            try
            {
                var json = File.ReadAllText(candidate);
                var result = JsonSerializer.Deserialize<T>(json, _jsonOptions);
                if (result is not null)
                {
                    return result;
                }
            }
            catch (Exception ex) when (
                ex is JsonException or IOException or UnauthorizedAccessException)
            {
                // 換下一個備份候選。
            }
        }

        return default;
    }

    private static IEnumerable<string> EnumerateReadCandidates(string path)
    {
        yield return path;
        for (var i = 1; i <= BackupCount; i++)
        {
            yield return $"{path}.bak{i}";
        }
    }

    /// <summary>
    /// 原子寫入：先寫暫存檔，輪替既有備份後，再以暫存檔替換主檔，避免寫入中途損毀主檔。
    /// </summary>
    private void WriteJson<T>(string path, T value)
    {
        var json = JsonSerializer.Serialize(value, _jsonOptions);
        var tmpPath = path + ".tmp";

        File.WriteAllText(tmpPath, json);
        RotateBackups(path);
        File.Move(tmpPath, path, overwrite: true);
    }

    /// <summary>
    /// 輪替備份，保留最後 <see cref="BackupCount"/> 份：<c>.bak(n-1) → .bak(n)</c>，
    /// 最後把目前主檔複製為 <c>.bak1</c>（複製而非移動，讓主檔在替換前始終存在）。
    /// </summary>
    private static void RotateBackups(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        for (var i = BackupCount - 1; i >= 1; i--)
        {
            var src = $"{path}.bak{i}";
            var dst = $"{path}.bak{i + 1}";
            if (File.Exists(src))
            {
                File.Move(src, dst, overwrite: true);
            }
        }

        File.Copy(path, $"{path}.bak1", overwrite: true);
    }

    /// <summary>
    /// <c>pet_data.json</c> 的檔案結構（持久化層 schema，非領域模型）：承載寵物清單與飼養上限。
    /// </summary>
    private sealed class PetDataFile
    {
        public List<Pet> Pets { get; set; } = new();
        public int MaxPetSlots { get; set; } = 2;
    }
}
