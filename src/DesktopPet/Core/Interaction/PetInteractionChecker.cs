using System.IO;
using System.Linq;
using System.Text.Json;
using DesktopPet.Core.Visuals;
using DesktopPet.Models;
// WPF 專案（UseWPF）的隱式 using 會帶入 System.Windows.Shapes.Path，與 System.IO.Path 撞名；
// 用別名固定為 System.IO.Path（與 Utils/StorageManager 等同慣例，勿移除）。
using Path = System.IO.Path;

namespace DesktopPet.Core.Interaction;

/// <summary>
/// 互動素材交集判定（設計檔 §6.5.2/§6.5.3）：兩隻寵物「共同擁有」的互動類型才會觸發互動；
/// 完全沒有交集則各自獨立行動，不報錯、不卡住（§6.5「漸進式增強」）。
/// </summary>
/// <remarks>
/// <b>檔名規則</b>（§6.5.2）：<c>interaction_[類型代號].png</c>，與該寵物的圖樣放在同一資料夾；
/// 每種類型<b>固定單張靜態圖</b>（不比照 §7.3 開放多張隨機——兩隻寵物各自抽圖會不同步，見設計檔
/// 理由），故本類別只判定「有沒有」，不像 <c>VisualRegistry.ScanUnits</c> 那樣建立多單元清單。
/// <para>
/// <b>類型清單</b>來自 <c>interaction_types.json</c>（§6.5.2：「開發者未來擴充新類型的方式」，
/// 新增類型不需改程式碼），缺檔／破損時退回 <see cref="DefaultTypes"/>
/// （<c>greet</c>/<c>play</c>/<c>cuddle</c>），與 <c>VisualRegistry.LoadFromFile</c> 同樣
/// 「不崩潰、漸進式增強」的精神。
/// </para>
/// <para>
/// 副檔名判定共用 <see cref="VisualRegistry.AllowedExtensions"/>（PNG/JPG/JPEG，§6.5.2「延續 6.4.3
/// 節的靜態圖片規格」），避免與 <c>anim_*</c> 的允許清單各自維護而漂移。純邏輯（只做檔案存在性
/// 檢查與 JSON 解析），不依賴 WPF，可跨平台單元測試。
/// </para>
/// </remarks>
public sealed class PetInteractionChecker
{
    /// <summary>§6.5.2 暫定的 3 種互動類型，<c>interaction_types.json</c> 缺檔／破損時的後援。</summary>
    public static IReadOnlyList<string> DefaultTypes { get; } = new[] { "greet", "play", "cuddle" };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IReadOnlyList<string> _registeredTypes;

    /// <param name="registeredTypes">已登記的互動類型清單；<c>null</c> 時使用 <see cref="DefaultTypes"/>。</param>
    public PetInteractionChecker(IReadOnlyList<string>? registeredTypes = null)
    {
        _registeredTypes = registeredTypes is { Count: > 0 } ? registeredTypes : DefaultTypes;
    }

    /// <summary>
    /// 讀取指定路徑的 <c>interaction_types.json</c>。檔案不存在／內容為空／無法解析時退回
    /// <see cref="DefaultTypes"/>，不丟例外（與 <c>VisualRegistry.LoadFromFile</c> 同慣例）。
    /// </summary>
    public static PetInteractionChecker LoadFromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return new PetInteractionChecker();

        try
        {
            var file = JsonSerializer.Deserialize<InteractionTypesFile>(File.ReadAllText(filePath), JsonOptions);
            var types = file?.Types?.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
            return new PetInteractionChecker(types);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new PetInteractionChecker();
        }
    }

    /// <summary>兩隻寵物「共同擁有」的互動類型清單（§6.5.3；可能為空、1 種、2 種或 3 種）。</summary>
    public IReadOnlyList<string> GetAvailableInteractionTypes(Pet petA, Pet petB)
    {
        ArgumentNullException.ThrowIfNull(petA);
        ArgumentNullException.ThrowIfNull(petB);

        var typesA = GetInteractionAssetTypes(petA.SkinFolderPath);
        var typesB = GetInteractionAssetTypes(petB.SkinFolderPath);
        return typesA.Intersect(typesB, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>只要有交集就能互動（§6.5.3）。</summary>
    public bool CanInteract(Pet petA, Pet petB) => GetAvailableInteractionTypes(petA, petB).Count > 0;

    /// <summary>
    /// 解析某隻寵物的圖樣資料夾中，某互動類型對應的實際檔案路徑（依 <see cref="VisualRegistry.AllowedExtensions"/>
    /// 依序嘗試）；缺圖時回傳 <c>null</c>。供 <see cref="GetInteractionAssetTypes"/> 判斷存在性，
    /// 也供 <c>PetCoordinator</c> 在決定播放某互動類型後取得實際圖片路徑，避免兩處各自維護一份掃描邏輯。
    /// </summary>
    public static string? ResolveInteractionImagePath(string skinFolderPath, string type)
    {
        if (string.IsNullOrWhiteSpace(skinFolderPath))
            return null;

        foreach (var ext in VisualRegistry.AllowedExtensions)
        {
            var candidate = Path.Combine(skinFolderPath, $"interaction_{type}{ext}");
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private List<string> GetInteractionAssetTypes(string skinFolderPath)
    {
        var found = new List<string>();
        foreach (var type in _registeredTypes)
        {
            if (ResolveInteractionImagePath(skinFolderPath, type) is not null)
                found.Add(type);
        }
        return found;
    }

    // interaction_types.json 的原始 DTO（§6.5.2：{ "types": ["greet", "play", "cuddle", ...] }）。
    private sealed class InteractionTypesFile
    {
        public List<string>? Types { get; set; }
    }
}
