using System.IO;
using System.Windows;
using DesktopPet.Core.Visuals;
using DesktopPet.Models;
using DesktopPet.UI;
using DesktopPet.Utils;
// WPF 專案（UseWPF）的隱式 using 會帶入 System.Windows.Shapes.Path，與 System.IO.Path 撞名；
// 用別名固定為 System.IO.Path（與 Utils/StorageManager 等同慣例，勿移除）。
using Path = System.IO.Path;

namespace DesktopPet;

/// <summary>
/// 應用程式進入點。
/// </summary>
/// <remarks>
/// <b>目前狀態：最小預覽接線，不是正式的 E2 啟動流程。</b>載入 <see cref="Settings"/>
/// （點穿模式／主題）並開一個視窗，讓 D1–D5 的成果（透明視窗、輸入、渲染、設置／關於）
/// 有畫面可驗證。
/// <para>
/// <b>刻意缺少</b>（皆屬尚未實作的 E1/E2/E4）：離線凍結（C1）、Onboarding 選飼養 1–2 隻、
/// <c>PetCoordinator</c> 管理多隻寵物、<c>StateManager</c>／<c>HappinessManager</c> 的 1 Hz
/// 狀態 tick、5 分鐘自動保存——因此心情固定 <see cref="PetVisualState.Neutral"/>
/// （<see cref="MainWindow.SetMood"/> 沒人呼叫）、右鍵「餵食」「睡眠」只會播對應動畫、
/// 不影響任何數值、SLEEP 也不會自動醒來。<see cref="StorageManager"/> 目前只用來讀寫
/// <see cref="Settings"/>（設置視窗儲存時），寵物清單／成就仍是空殼；完整存讀（含離線凍結）
/// 由 E4 接手。這段代碼預期在 E2 完整實作「載入存檔 → 離線凍結 → 決定飼養數量 →
/// 建立 PetCoordinator/PetInstance」後被取代。
/// </para>
/// </remarks>
public partial class App : Application
{
    private StorageManager? _storage;
    private GameState? _state;
    private VisualRegistry? _registry;
    private MainWindow? _window;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _storage = new StorageManager();
        _state = _storage.Load(); // 缺檔／首次啟動時回傳全新預設值（含 Settings 的內建預設）。

        // Resources/ 隨程式輸出（csproj 的 CopyToOutputDirectory），故以執行檔所在目錄還原路徑。
        string resourcesDir = Path.Combine(AppContext.BaseDirectory, "Resources");

        // pet_visuals.json 缺檔／破損時 LoadFromFile 會自動退回標準 6 類型定義（§7.3.3），不會丟例外。
        _registry = VisualRegistry.LoadFromFile(Path.Combine(resourcesDir, "pet_visuals.json"));

        _window = new MainWindow
        {
            ClickThrough = _state.Settings.ClickThrough,
        };
        _window.LoadSkin(GetSkinFolderPath(_state.Settings.Theme), _registry);
        _window.MenuActionRequested += OnMenuActionRequested;
        _window.Show();
    }

    /// <summary>
    /// D5：設置／關於視窗接線（§6.3）。其餘 3 項（玩耍/清潔/退出）留給尚未建立的 E1/E2/E4
    /// （退出已由 <see cref="MainWindow.OnExitMenuClick"/> 直接處理，不會走到這裡）。
    /// </summary>
    private void OnMenuActionRequested(object? sender, PetMenuAction action)
    {
        switch (action)
        {
            case PetMenuAction.Settings:
                OpenSettings();
                break;
            case PetMenuAction.About:
                new AboutWindow { Owner = _window }.ShowDialog();
                break;
        }
    }

    private void OpenSettings()
    {
        var dialog = new SettingsWindow(_state!.Settings) { Owner = _window };
        if (dialog.ShowDialog() != true)
            return; // 取消：SettingsWindow 保證未觸碰 _state.Settings。

        _window!.ClickThrough = _state.Settings.ClickThrough;
        _window.LoadSkin(GetSkinFolderPath(_state.Settings.Theme), _registry!);
        _storage!.Save(_state); // 只有設置視窗會改動 _state，此時存檔不會覆蓋掉其他未實作的欄位。
    }

    private static string GetSkinFolderPath(string themeName) =>
        Path.Combine(AppContext.BaseDirectory, "Resources", "Assets", "Themes", themeName);
}
