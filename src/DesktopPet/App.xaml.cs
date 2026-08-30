using System.IO;
using System.Windows;
using DesktopPet.Core;
using DesktopPet.Core.Interaction;
using DesktopPet.Core.Visuals;
using DesktopPet.Models;
using DesktopPet.UI;
// WPF 專案（UseWPF）的隱式 using 會帶入 System.Windows.Shapes.Path，與 System.IO.Path 撞名；
// 用別名固定為 System.IO.Path（與 Utils/StorageManager 等同慣例，勿移除）。
using Path = System.IO.Path;

namespace DesktopPet;

/// <summary>
/// 應用程式進入點。
/// </summary>
/// <remarks>
/// <b>目前狀態：最小預覽接線，不是正式的 E4 啟動流程。</b>以 <see cref="OnboardingWindow"/>（E2）
/// 詢問飼養數量，依答案建立 1–2 隻 <see cref="Pet"/> 並交給 <see cref="PetCoordinator"/>（E2/E3）
/// 統一啟動，讓 A～E3 群的成果（透明視窗、輸入、渲染、1 Hz 狀態 tick、心情判定、幸福度、
/// 多寵物視窗管理、雙寵物互動）串成一個可實跑的流程。雙寵物模式下 <see cref="PetCoordinator"/>
/// 會依 <c>interaction_types.json</c> 與兩隻預覽寵物皆內建的 <c>interaction_*.png</c> 素材，
/// 自動判定 greet/cuddle、並讓右鍵「玩耍」可手動觸發 play（§6.5）。
/// <para>
/// <b>刻意缺少</b>（屬尚未實作的 E4）：讀存檔／離線凍結（每次啟動皆是全新寵物，起始值直接寫死於
/// 本檔）、右鍵「餵食」「睡眠」的實際扣值效果與 SLEEP 自動醒來、自動保存、關閉視窗前存檔。
/// 這段代碼預期在 E4 完整實作「載入存檔 → 離線凍結 → （無存檔時）Onboarding → 建立
/// PetCoordinator」後被取代。
/// </para>
/// </remarks>
public partial class App : Application
{
    /// <summary>
    /// 預覽固定使用的內建主題資料夾名稱（§6.4.1 兩套內建主題）。第 2 隻寵物換一套主題，
    /// 純粹方便預覽時目視區分兩隻；正式版由 <c>Pet.SkinFolderPath</c>／使用者選擇決定，屬 Phase 2。
    /// </summary>
    private static readonly string[] PreviewThemeNames = { "builtin_cat", "builtin_dog" };

    private PetCoordinator? _coordinator;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Onboarding 對話框關閉時（尚未顯示任何寵物視窗）不能被 WPF 預設的
        // ShutdownMode.OnLastWindowClose 誤判成「最後一個視窗關閉」而提前結束整個 App（§6.5.1）。
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Resources/ 隨程式輸出（csproj 的 CopyToOutputDirectory），故以執行檔所在目錄還原路徑。
        string resourcesDir = Path.Combine(AppContext.BaseDirectory, "Resources");

        // pet_visuals.json 缺檔／破損時 LoadFromFile 會自動退回標準 6 類型定義（§7.3.3），不會丟例外。
        var registry = VisualRegistry.LoadFromFile(Path.Combine(resourcesDir, "pet_visuals.json"));

        // interaction_types.json 同理退回 greet/play/cuddle 三種預設類型（§6.5.2）；單寵物模式用不到。
        var interactionChecker = PetInteractionChecker.LoadFromFile(Path.Combine(resourcesDir, "interaction_types.json"));

        var onboarding = new OnboardingWindow();
        int petCount = onboarding.ShowDialog() == true ? onboarding.SelectedPetCount : 1;

        var pets = new List<Pet>(petCount);
        for (int i = 0; i < petCount; i++)
            pets.Add(CreatePreviewPet(i, resourcesDir));

        _coordinator = new PetCoordinator(pets, registry, interactionChecker);
        _coordinator.Start();
    }

    /// <summary>建立一隻寫死起始值的預覽寵物（無存檔機制前的替代品，見類別註解）。</summary>
    private static Pet CreatePreviewPet(int index, string resourcesDir)
    {
        string themeName = PreviewThemeNames[index % PreviewThemeNames.Length];
        var now = DateTime.Now;
        return new Pet
        {
            Id = $"preview_{index + 1}",
            Name = $"Preview {index + 1}",
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
