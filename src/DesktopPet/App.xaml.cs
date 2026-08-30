using System.IO;
using System.Linq;
using System.Windows;
using DesktopPet.Core;
using DesktopPet.Core.Interaction;
using DesktopPet.Core.Visuals;
using DesktopPet.Models;
using DesktopPet.UI;
using DesktopPet.Utils;
// WPF 專案（UseWPF）的隱式 using 會帶入 System.Windows.Shapes.Path，與 System.IO.Path 撞名；
// 用別名固定為 System.IO.Path（與 Utils/StorageManager 等同慣例，勿移除）。
using Path = System.IO.Path;

namespace DesktopPet;

/// <summary>
/// 應用程式進入點：Phase 1 MVP 的完整啟動流程（設計檔 §7.1 步驟 0、§8.2）。
/// </summary>
/// <remarks>
/// <see cref="OnStartup"/> 依序：
/// <list type="number">
///   <item><description>載入 <c>pet_visuals.json</c>／<c>interaction_types.json</c>（B7/E3，
///     缺檔／破損皆有預設後援，不會丟例外）。</description></item>
///   <item><description><see cref="StorageManager.Load"/> 讀存檔。<c>Pets</c> 為空（首次啟動或
///     存檔被清空）才顯示 <see cref="OnboardingWindow"/>（E2）詢問飼養數量並建立新寵物；否則直接
///     沿用存檔內容，不重複詢問。</description></item>
///   <item><description><see cref="OfflineFreezeHandler.Apply(IEnumerable{Pet})"/>：僅重設
///     <c>LastTickTime</c>，四項數值全部凍結（§7.4.4）——新建的寵物也一併套用，效果等同於「剛好
///     在此刻建立」。</description></item>
///   <item><description>建立 <see cref="PetCoordinator"/>（E1/E2/E3）並 <see cref="PetCoordinator.Start"/>；
///     依 <see cref="Settings.ClickThrough"/> 套用每個視窗的點穿模式。</description></item>
///   <item><description><see cref="StorageManager.StartAutoSave"/> 掛上 5 分鐘自動保存（§8.2）；
///     <see cref="OnExit"/> 另外做關閉前保存。</description></item>
/// </list>
/// <para>
/// <b>刻意缺少</b>（屬 Phase 2）：新增/送走寵物、設置面板（<c>Settings</c> 其餘欄位如
/// <c>AlwaysOnTop</c>／<c>Theme</c>／語言／音效皆已載入但尚無 UI 可變更）、「清潔」的數值效果
/// （設計檔未定義）。
/// </para>
/// </remarks>
public partial class App : Application
{
    /// <summary>新寵物預設使用的內建主題資料夾名稱（§6.4.1 兩套內建主題），依索引輪流指派。</summary>
    private static readonly string[] DefaultThemeNames = { "builtin_cat", "builtin_dog" };

    /// <summary>新寵物預設名稱，取自設計檔 §5.2 存檔範例的兩隻寵物名。</summary>
    private static readonly string[] DefaultPetNames = { "Fluffy", "Mochi" };

    private StorageManager? _storage;
    private GameState? _state;
    private PetCoordinator? _coordinator;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Onboarding 對話框關閉時（尚未顯示任何寵物視窗）不能被 WPF 預設的
        // ShutdownMode.OnLastWindowClose 誤判成「最後一個視窗關閉」而提前結束整個 App（§6.5.1）。
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Resources/ 隨程式輸出（csproj 的 CopyToOutputDirectory），故以執行檔所在目錄還原路徑。
        string resourcesDir = Path.Combine(AppContext.BaseDirectory, "Resources");

        // pet_visuals.json / interaction_types.json 缺檔／破損時皆自動退回標準預設，不會丟例外。
        var registry = VisualRegistry.LoadFromFile(Path.Combine(resourcesDir, "pet_visuals.json"));
        var interactionChecker = PetInteractionChecker.LoadFromFile(Path.Combine(resourcesDir, "interaction_types.json"));

        _storage = new StorageManager();
        _state = _storage.Load();

        if (_state.Pets.Count == 0)
        {
            // 首次啟動（或存檔被清空）：詢問要養幾隻（§6.5.1），建立全新寵物。
            var onboarding = new OnboardingWindow();
            int petCount = onboarding.ShowDialog() == true ? onboarding.SelectedPetCount : 1;
            for (int i = 0; i < petCount; i++)
                _state.Pets.Add(CreateNewPet(i, resourcesDir));
        }
        else if (_state.Pets.Count > PetCoordinator.MaxPets)
        {
            // 防禦：存檔異常超過飼養上限時裁切（§5.1 固定 2 隻），避免 PetCoordinator 建構式丟例外。
            _state.Pets = _state.Pets.Take(PetCoordinator.MaxPets).ToList();
        }

        // §7.4.4：離線期間四項數值全部凍結，僅重設 LastTickTime；新建的寵物一併套用等同「剛建立」。
        new OfflineFreezeHandler().Apply(_state.Pets);

        _coordinator = new PetCoordinator(_state.Pets, registry, interactionChecker);
        foreach (var instance in _coordinator.Instances)
            instance.Window.ClickThrough = _state.Settings.ClickThrough;

        _coordinator.Start();

        // §8.2：每 5 分鐘自動保存。stateProvider 在背景執行緒被呼叫，故 marshal 回 UI 執行緒取快照，
        // 避免與 1 Hz 狀態 tick 同時讀寫同一個 Pet 物件（見 PetCoordinator.SnapshotPets 註解）。
        var storage = _storage;
        var state = _state;
        var coordinator = _coordinator;
        storage.StartAutoSave(() => Dispatcher.Invoke(() => BuildSaveSnapshot(state, coordinator)));
    }

    /// <summary>組出可安全拿去序列化的存檔快照：寵物清單為深拷貝，設定／成就目前無執行期變更故直接沿用。</summary>
    private static GameState BuildSaveSnapshot(GameState template, PetCoordinator coordinator) => new()
    {
        Pets = coordinator.SnapshotPets(),
        MaxPetSlots = template.MaxPetSlots,
        Achievements = template.Achievements,
        Settings = template.Settings,
    };

    /// <summary>關閉前保存（§8.2）。已在 UI 執行緒，不需 marshal。保存失敗不阻擋程式結束。</summary>
    protected override void OnExit(ExitEventArgs e)
    {
        if (_storage is not null && _state is not null && _coordinator is not null)
        {
            try
            {
                _storage.Save(BuildSaveSnapshot(_state, _coordinator));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 關閉前存檔失敗（磁碟滿／權限不足等）不應阻止程式結束。
            }

            _storage.Dispose(); // 停止自動保存計時器。
        }

        _coordinator?.Dispose();

        base.OnExit(e);
    }

    /// <summary>建立一隻全新寵物（首次啟動／存檔為空時使用）。</summary>
    private static Pet CreateNewPet(int index, string resourcesDir)
    {
        string themeName = DefaultThemeNames[index % DefaultThemeNames.Length];
        string name = DefaultPetNames[index % DefaultPetNames.Length];
        var now = DateTime.Now;
        return new Pet
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            CreatedDate = now,
            Hunger = 50,
            Happiness = 80,
            Energy = 70,
            Health = 100,
            CurrentMood = PetMood.Neutral,
            LastFedTime = now,
            LastInteractionTime = now,
            LastTickTime = now,
            SkinId = themeName,
            SkinSourceType = "builtin",
            SkinFolderPath = Path.Combine(resourcesDir, "Assets", "Themes", themeName),
        };
    }
}
