using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
// WPF 專案（UseWPF）的隱式 using 會帶入 System.Windows.Shapes.Path，與 System.IO.Path 撞名；
// 用別名固定為 System.IO.Path（與 Utils/StorageManager 同慣例，勿移除）。
using Path = System.IO.Path;

namespace DesktopPet.Core.Visuals;

/// <summary>
/// 視覺類型登記表（設計檔 §7.3.3）：載入 <c>pet_visuals.json</c> 的類型清單，並掃描圖樣資料夾
/// 建立「每個狀態有哪些動畫單元」的索引（供 <c>PetVisualSelector</c>（B5）的 <c>_pool</c> 使用）。
/// </summary>
/// <remarks>
/// <b>資料驅動：</b>新增／調整圖片類型只改 <c>pet_visuals.json</c>，不需重編譯（§7.3.3）。
/// <b>心情代號與前綴非一對一</b>（<c>LOW_ENERGY → anim_tired</c>），故掃描一律以設定檔的
/// <see cref="VisualTypeDefinition.Prefix"/> 為準，不用列舉名推導。
/// <para>
/// <see cref="LoadFromFile"/> 在檔案不存在／無法解析時退回 <see cref="DefaultDefinitions"/>
/// （設計檔 §7.3.3 的 6 種標準類型），維持「不崩潰、漸進式增強」的一致精神；B7 會將同一份
/// 標準類型以 <c>pet_visuals.json</c> 隨程式發佈。本類別不依賴 WPF，可跨平台單元測試。
/// </para>
/// </remarks>
public sealed class VisualRegistry
{
    /// <summary>
    /// 允許的圖片副檔名（含點）。<c>internal</c>：D4（<c>Core/AnimationManager.cs</c>）需要用同一份
    /// 清單，把 <see cref="ScanUnits"/> 回傳的「去副檔名單元名」還原為實際檔案路徑，
    /// 避免兩處各自維護一份清單而漂移。
    /// </summary>
    internal static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    // UPPER_SNAKE 代號 ↔ PetVisualState（由列舉自動產生，確保與 ToCode 一致；讀取容忍大小寫）。
    private static readonly IReadOnlyDictionary<string, PetVisualState> CodeToState =
        Enum.GetValues<PetVisualState>().ToDictionary(ToCode, v => v, StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<PetVisualState, VisualTypeDefinition> _byState;

    /// <summary>所有已登記的視覺類型定義（依載入順序）。</summary>
    public IReadOnlyList<VisualTypeDefinition> Definitions { get; }

    private VisualRegistry(IReadOnlyList<VisualTypeDefinition> definitions)
    {
        Definitions = definitions;
        _byState = new Dictionary<PetVisualState, VisualTypeDefinition>();
        foreach (var def in definitions)
            _byState[def.State] = def; // 同代號重複時後者覆蓋前者
    }

    // ── 建立 ─────────────────────────────────────────────────────

    /// <summary>由已解析的定義集合建立登記表。</summary>
    public static VisualRegistry FromDefinitions(IEnumerable<VisualTypeDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        return new VisualRegistry(definitions.ToList());
    }

    /// <summary>解析 <c>pet_visuals.json</c> 內容字串為登記表（無法解析的條目略過，不丟例外）。</summary>
    public static VisualRegistry LoadFromJson(string json)
    {
        var defs = ParseDefinitions(json);
        return new VisualRegistry(defs.Count > 0 ? defs : DefaultDefinitions());
    }

    /// <summary>
    /// 讀取指定路徑的 <c>pet_visuals.json</c>。檔案不存在／內容為空／無法解析時退回
    /// <see cref="DefaultDefinitions"/>（§7.3.3 標準 6 類型），不丟例外。
    /// </summary>
    public static VisualRegistry LoadFromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return new VisualRegistry(DefaultDefinitions());

        try
        {
            return LoadFromJson(File.ReadAllText(filePath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new VisualRegistry(DefaultDefinitions());
        }
    }

    // ── 查詢 ─────────────────────────────────────────────────────

    /// <summary>取得某狀態的定義；未登記則回 <c>null</c>。</summary>
    public VisualTypeDefinition? GetDefinition(PetVisualState state) =>
        _byState.GetValueOrDefault(state);

    /// <summary>是否已登記某狀態。</summary>
    public bool Contains(PetVisualState state) => _byState.ContainsKey(state);

    // ── 掃描資料夾建索引（§7.3.3）─────────────────────────────────

    /// <summary>
    /// 掃描圖樣資料夾，為每個已登記狀態建立其存在的動畫單元清單（單元名 = 檔名去副檔名，
    /// 如 <c>anim_idle_1</c>），依序號遞增排序。只回傳至少有一個單元的狀態
    /// （呼應 <c>PetVisualSelector</c> 對空清單走 fallback 的處理）。
    /// </summary>
    /// <remarks>
    /// 只掃描檔名建索引、不解碼點陣圖（§7.3.6 延遲載入）。比對規則：檔名須為
    /// <c>{prefix}_{序號}.{png|jpg|jpeg}</c>；序號不要求連號（§7.3.3）；同資料夾的
    /// <c>interaction_*.png</c>（§6.5.2）與其他檔案自然不符任何 <c>anim_</c> 前綴而被略過。
    /// </remarks>
    public IReadOnlyDictionary<PetVisualState, IReadOnlyList<string>> ScanUnits(string skinFolderPath)
    {
        var pool = new Dictionary<PetVisualState, IReadOnlyList<string>>();
        if (string.IsNullOrWhiteSpace(skinFolderPath) || !Directory.Exists(skinFolderPath))
            return pool;

        var files = Directory.GetFiles(skinFolderPath);

        foreach (var def in _byState.Values)
        {
            var regex = new Regex($"^{Regex.Escape(def.Prefix)}_(\\d+)$", RegexOptions.IgnoreCase);
            var matched = new List<(int index, string name)>();

            foreach (var file in files)
            {
                if (!AllowedExtensions.Contains(Path.GetExtension(file)))
                    continue;

                var name = Path.GetFileNameWithoutExtension(file);
                var m = regex.Match(name);
                if (m.Success && int.TryParse(m.Groups[1].Value, out int index))
                    matched.Add((index, name));
            }

            if (matched.Count > 0)
                pool[def.State] = matched.OrderBy(u => u.index).Select(u => u.name).ToList();
        }

        return pool;
    }

    // ── 標準 6 類型（§7.3.3）：pet_visuals.json 缺檔／破損時的內建後援 ────────

    /// <summary>設計檔 §7.3.3 的標準 6 種視覺類型（B7 會以同內容的 <c>pet_visuals.json</c> 發佈）。</summary>
    public static List<VisualTypeDefinition> DefaultDefinitions() => new()
    {
        new() { State = PetVisualState.Sad,       Kind = VisualKind.Mood,  Prefix = "anim_sad",   Required = false, Fallback = PetVisualState.Neutral,   RerollIntervalSec = 0 },
        new() { State = PetVisualState.LowEnergy, Kind = VisualKind.Mood,  Prefix = "anim_tired", Required = false, Fallback = PetVisualState.Neutral,   RerollIntervalSec = 0 },
        new() { State = PetVisualState.Neutral,   Kind = VisualKind.Mood,  Prefix = "anim_idle",  Required = true,  Fallback = null,                     RerollIntervalSec = 8 },
        new() { State = PetVisualState.Click,     Kind = VisualKind.Event, Prefix = "anim_click", Required = false, Fallback = null,                     DurationSec = 1.5 },
        new() { State = PetVisualState.Feed,      Kind = VisualKind.Event, Prefix = "anim_feed",  Required = false, Fallback = null,                     DurationSec = 2.5 },
        new() { State = PetVisualState.Sleep,     Kind = VisualKind.Event, Prefix = "anim_sleep", Required = false, Fallback = PetVisualState.LowEnergy, DurationSec = 0, RerollIntervalSec = 20 },
    };

    // ── 內部：解析與代號對映 ──────────────────────────────────────

    private static List<VisualTypeDefinition> ParseDefinitions(string json)
    {
        var result = new List<VisualTypeDefinition>();
        if (string.IsNullOrWhiteSpace(json))
            return result;

        PetVisualsFile? file;
        try
        {
            file = JsonSerializer.Deserialize<PetVisualsFile>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return result; // 破損 → 交由呼叫端退回 Default
        }

        if (file?.Visuals is null)
            return result;

        foreach (var entry in file.Visuals)
        {
            if (entry?.Code is null || !CodeToState.TryGetValue(entry.Code.Trim(), out var state))
                continue; // 未知代號略過，不崩潰

            PetVisualState? fallback = null;
            if (entry.Fallback is not null && CodeToState.TryGetValue(entry.Fallback.Trim(), out var fb))
                fallback = fb;

            result.Add(new VisualTypeDefinition
            {
                State = state,
                Kind = string.Equals(entry.Kind, "event", StringComparison.OrdinalIgnoreCase)
                    ? VisualKind.Event
                    : VisualKind.Mood,
                Prefix = entry.Prefix ?? string.Empty,
                Required = entry.Required ?? false,
                Fallback = fallback,
                DurationSec = entry.DurationSec ?? 0,
                RerollIntervalSec = entry.RerollIntervalSec ?? 0,
            });
        }

        return result;
    }

    /// <summary>PetVisualState → UPPER_SNAKE 代號（<c>LowEnergy → "LOW_ENERGY"</c>、<c>Neutral → "NEUTRAL"</c>）。</summary>
    public static string ToCode(PetVisualState state)
    {
        var name = state.ToString();
        var sb = new StringBuilder(name.Length + 2);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (i > 0 && char.IsUpper(c)) sb.Append('_');
            sb.Append(char.ToUpperInvariant(c));
        }
        return sb.ToString();
    }

    // pet_visuals.json 的原始 DTO（全字串欄位，避免與共用列舉轉換策略衝突）。
    private sealed class PetVisualsFile
    {
        public List<VisualEntry>? Visuals { get; set; }
        // weather 區塊（§7.5，Phase 2）此處不解析，System.Text.Json 會自動忽略未知欄位。
    }

    private sealed class VisualEntry
    {
        public string? Code { get; set; }
        public string? Kind { get; set; }
        public string? Prefix { get; set; }
        public bool? Required { get; set; }
        public string? Fallback { get; set; }
        public double? DurationSec { get; set; }
        public int? RerollIntervalSec { get; set; }
    }
}
