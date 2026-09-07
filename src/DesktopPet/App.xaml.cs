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
/// 應用程式進入點與生命週期管理（E4，設計檔 §3.1 / §7.1 / §8.2）。啟動流程：
/// <list type="number">
///   <item><description>載入資料驅動設定（<c>pet_visuals.json</c> / <c>interaction_types.json</c>）。</description></item>
///   <item><description>從 <c>%APPDATA%\DesktopPet\</c> 載入存檔（<see cref="StorageManager.Load"/>）。</description></item>
///   <item><description>首次啟動（無寵物）→ <see cref="OnboardingWindow"/> 詢問養 1/2 隻並建立寵物（§6.5.1）。</description></item>
///   <item><description>離線凍結（<see cref="OfflineFreezeHandler"/>）：四項數值全凍結，僅重設 tick 基準（§7.4.4）。</description></item>
///   <item><description>建立並啟動 <see cref="PetCoordinator"/>（每隻一視窗、1 Hz 狀態 tick、雙寵物互動）。</description></item>
///   <item><description>啟動 5 分鐘自動保存（§8.2）；於 <see cref="OnExit"/> 停止自動保存並做關閉前保存。</description></item>
/// </list>
/// </summary>
public partial class App : Application
{
    private StorageManager? _storage;
    private PetCoordinator? _coordinator;
    private GameState? _state;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Resources/ 隨程式輸出（csproj 的 CopyToOutputDirectory），故以執行檔所在目錄還原路徑。
        string resourcesDir = Path.Combine(AppContext.BaseDirectory, "Resources");
        string themesDir = Path.Combine(resourcesDir, "Assets", "Themes");

        // 資料驅動設定：缺檔/破損時各自退回內建預設（§7.3.3 / §6.5.3），不丟例外。
        var registry = VisualRegistry.LoadFromFile(Path.Combine(resourcesDir, "pet_visuals.json"));
        var checker = PetInteractionChecker.LoadFromFile(Path.Combine(resourcesDir, "interaction_types.json"));

        // 1) 載入存檔（首次啟動回傳全新預設 GameState，Pets 為空）。
        _storage = new StorageManager();
        GameState state = _storage.Load();
        _state = state;

        // 2) 首次啟動：Onboarding 決定飼養數量並建立寵物（§6.5.1）。
        if (state.Pets.Count == 0)
        {
            int count = AskPetCount();
            state.Pets = PetFactory.CreateInitialPets(count, themesDir, DateTime.Now);
        }

        // 3) 離線凍結：四項數值全凍結，僅將各寵物 LastTickTime 重設為現在（§7.4.4）。
        new OfflineFreezeHandler().Apply(state.Pets);

        // 4) 建立並啟動協調層（每隻一視窗、各自 1 Hz 狀態 tick、雙寵物互動檢查）。
        _coordinator = new PetCoordinator(state, registry, checker);
        _coordinator.Start();

        // 5) 每 5 分鐘自動保存當前狀態（§8.2）。狀態就地承載於 state，直接提供快照。
        _storage.StartAutoSave(() => state);
    }

    /// <summary>顯示 Onboarding 並回傳飼養數量（1 或 2）；使用者直接關閉時採預設 1 隻（§6.5.1）。</summary>
    private static int AskPetCount()
    {
        var onboarding = new OnboardingWindow();
        onboarding.ShowDialog();
        return onboarding.SelectedCount;
    }

    /// <summary>關閉前：停止自動保存並做最後一次保存（§8.2），確保未達自動保存週期的變更不遺失。</summary>
    protected override void OnExit(ExitEventArgs e)
    {
        _coordinator?.Dispose();

        if (_storage is not null)
        {
            _storage.StopAutoSave();
            try
            {
                if (_state is not null)
                    _storage.Save(_state);
            }
            catch
            {
                // 關閉前保存失敗（磁碟滿/權限）不阻斷退出；上一輪自動保存或備份仍在（§8.2）。
            }

            _storage.Dispose();
        }

        base.OnExit(e);
    }
}
