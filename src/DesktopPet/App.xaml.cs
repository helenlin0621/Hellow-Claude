using System.Windows;

namespace DesktopPet;

/// <summary>
/// 應用程式進入點。
///
/// A1 專案骨架僅提供最小可編譯的進入點；實際的執行流程
/// （載入存檔 → 離線凍結 → 決定飼養 1/2 隻 → 建立 <c>PetCoordinator</c>
/// 與各 <c>PetInstance</c> 視窗）會在後續任務（E2/E4）於此接上。
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // TODO(E2): 建立 PetCoordinator，載入存檔、套用離線凍結、
        // 依設定或 Onboarding 決定飼養數量，並啟動寵物實例。
    }
}
