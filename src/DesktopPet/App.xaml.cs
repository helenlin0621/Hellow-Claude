using System.IO;
using System.Windows;
using DesktopPet.Core;
using DesktopPet.Core.Visuals;
using DesktopPet.Models;
// WPF 專案（UseWPF）的隱式 using 會帶入 System.Windows.Shapes.Path，與 System.IO.Path 撞名；
// 用別名固定為 System.IO.Path（與 Utils/StorageManager 等同慣例，勿移除）。
using Path = System.IO.Path;

namespace DesktopPet;

/// <summary>
/// 應用程式進入點。
/// </summary>
/// <remarks>
/// <b>目前狀態：最小預覽接線，不是正式的 E2 啟動流程。</b>建立單一 <see cref="Pet"/> 與對應的
/// <see cref="PetInstance"/>（E1），讓 A～D 群的成果（透明視窗、輸入、渲染、1 Hz 狀態 tick、
/// 心情判定、幸福度）串成一個可實跑的單元。
/// <para>
/// <b>刻意缺少</b>（皆屬尚未實作的 E2/E4）：讀存檔／離線凍結（每次啟動皆是全新寵物，起始值
/// 直接寫死於本檔）、Onboarding 選飼養 1–2 隻、<c>PetCoordinator</c> 管理多隻寵物、右鍵
/// 「餵食」「睡眠」的實際扣值效果與 SLEEP 自動醒來、自動保存、關閉視窗前存檔。這段代碼預期
/// 在 E2 完整實作「載入存檔 → 離線凍結 → 決定飼養數量 → 建立 PetCoordinator」後被取代。
/// </para>
/// </remarks>
public partial class App : Application
{
    /// <summary>
    /// 預覽固定使用的內建主題資料夾名稱（§6.4.1 兩套內建主題之一）。
    /// 正式版由 <c>Pet.SkinFolderPath</c>／<c>Settings.Theme</c> 決定要載入哪一套，屬 E2。
    /// </summary>
    private const string PreviewThemeName = "builtin_cat";

    private PetInstance? _petInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Resources/ 隨程式輸出（csproj 的 CopyToOutputDirectory），故以執行檔所在目錄還原路徑。
        string resourcesDir = Path.Combine(AppContext.BaseDirectory, "Resources");

        // pet_visuals.json 缺檔／破損時 LoadFromFile 會自動退回標準 6 類型定義（§7.3.3），不會丟例外。
        var registry = VisualRegistry.LoadFromFile(Path.Combine(resourcesDir, "pet_visuals.json"));
        string skinFolderPath = Path.Combine(resourcesDir, "Assets", "Themes", PreviewThemeName);

        var now = DateTime.Now;
        var pet = new Pet
        {
            Id = "preview",
            Name = "Preview",
            CreatedDate = now,
            Hunger = 50,
            Happiness = 80,
            Energy = 70,
            Health = 100,
            CurrentMood = PetMood.Neutral,
            LastFedTime = now,
            LastInteractionTime = now,
            LastTickTime = now,
            SkinId = PreviewThemeName,
            SkinSourceType = "builtin",
            SkinFolderPath = skinFolderPath,
        };

        _petInstance = new PetInstance(pet, registry);
        _petInstance.Start();
    }
}
