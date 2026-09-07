using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopPet.Models;
using DesktopPet.Utils;
using Path = System.IO.Path;

namespace DesktopPet.Core.Interaction;

/// <summary>
/// 雙寵物互動素材檢測（設計檔 §6.5.2 / §6.5.3）。純邏輯（僅讀檔名索引），可跨平台單元測試。
/// 依 <c>interaction_types.json</c> 登記的類型清單，掃描每隻寵物圖樣資料夾中的
/// <c>interaction_{類型}.png</c>，以「交集非空」判定能否互動——<b>漸進式增強</b>：缺素材各自獨立，不報錯（§6.5）。
/// </summary>
/// <remarks>
/// <b>核心不變量（§6.5.2）：</b>互動素材<b>固定單張靜態 PNG</b>（不比照 §7.3 開放多張隨機），
/// 避免雙方各自抽圖不同步。觸發門檻是雙方互動類型的<b>交集</b>非空。類型清單純資料驅動，
/// 新增類型只需改 <c>interaction_types.json</c>，不需重編譯（§6.5.2）。
/// </remarks>
public sealed class PetInteractionChecker
{
    /// <summary>互動素材檔名前綴（§6.5.2：<c>interaction_[類型代號].png</c>）。</summary>
    public const string FileNamePrefix = "interaction_";

    /// <summary>互動素材副檔名（§6.5.2：固定單張靜態 PNG）。</summary>
    public const string FileExtension = ".png";

    /// <summary>設定檔缺漏／破損時的後備類型清單（§6.5.2 初始三種）。</summary>
    private static readonly string[] DefaultTypes = { "greet", "play", "cuddle" };

    private readonly IReadOnlyList<string> _knownTypes;

    /// <param name="knownTypes">
    /// 已登記的互動類型代號（來自 <c>interaction_types.json</c>）。空集合代表停用互動檢測
    /// （<see cref="GetAvailableInteractionTypes"/> 一律回傳空）。
    /// </param>
    public PetInteractionChecker(IEnumerable<string> knownTypes)
    {
        ArgumentNullException.ThrowIfNull(knownTypes);
        // 去重、去空白，維持登記順序（供 UI 動態產生上傳欄位時的一致排序，§6.5.2）。
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();
        foreach (var t in knownTypes)
        {
            if (!string.IsNullOrWhiteSpace(t) && seen.Add(t))
                list.Add(t);
        }

        _knownTypes = list;
    }

    /// <summary>目前登記的互動類型（唯讀）。</summary>
    public IReadOnlyList<string> KnownTypes => _knownTypes;

    /// <summary>
    /// 從 <c>interaction_types.json</c> 載入類型清單並建立檢測器。檔案缺漏／破損時退回
    /// <see cref="DefaultTypes"/>（§6.5.3「不報錯」）。
    /// </summary>
    /// <param name="jsonPath"><c>interaction_types.json</c> 絕對路徑。</param>
    public static PetInteractionChecker LoadFromFile(string jsonPath)
    {
        ArgumentNullException.ThrowIfNull(jsonPath);

        try
        {
            if (File.Exists(jsonPath))
            {
                var json = File.ReadAllText(jsonPath);
                var file = JsonSerializer.Deserialize<InteractionTypesFile>(json, StorageManager.JsonOptions);
                if (file?.Types is { Count: > 0 })
                    return new PetInteractionChecker(file.Types);
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // 破損／讀取失敗：退回預設類型，不讓互動檢測阻斷啟動。
        }

        return new PetInteractionChecker(DefaultTypes);
    }

    /// <summary>
    /// 掃描單一寵物擁有的互動素材類型（§6.5.3 <c>GetInteractionAssetTypes</c>）：在其
    /// <see cref="Pet.SkinFolderPath"/> 中，對每個已登記類型檢查是否有 <c>interaction_{類型}.png</c>。
    /// 資料夾不存在時回傳空清單（缺素材＝不觸發，不報錯）。
    /// </summary>
    public IReadOnlyList<string> GetInteractionAssetTypes(Pet pet)
    {
        ArgumentNullException.ThrowIfNull(pet);

        var owned = new List<string>();
        if (string.IsNullOrEmpty(pet.SkinFolderPath) || !Directory.Exists(pet.SkinFolderPath))
            return owned;

        foreach (var type in _knownTypes)
        {
            var path = Path.Combine(pet.SkinFolderPath, FileNamePrefix + type + FileExtension);
            if (File.Exists(path))
                owned.Add(type);
        }

        return owned;
    }

    /// <summary>
    /// 兩隻寵物「共同擁有」的互動類型（§6.5.3）：各自素材類型的交集，維持登記順序。
    /// 可能為空（無交集＝各自獨立行動）。
    /// </summary>
    public List<string> GetAvailableInteractionTypes(Pet petA, Pet petB)
    {
        ArgumentNullException.ThrowIfNull(petA);
        ArgumentNullException.ThrowIfNull(petB);

        var typesB = new HashSet<string>(GetInteractionAssetTypes(petB), StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var type in GetInteractionAssetTypes(petA))
        {
            if (typesB.Contains(type))
                result.Add(type);
        }

        return result;
    }

    /// <summary>雙方是否有任一共同互動類型（§6.5.3 <c>CanInteract</c>）。</summary>
    public bool CanInteract(Pet petA, Pet petB) => GetAvailableInteractionTypes(petA, petB).Count > 0;

    /// <summary>
    /// 由單元名還原互動素材絕對路徑：<c>{SkinFolderPath}/interaction_{type}.png</c>（§6.5.2 命名規則）。
    /// </summary>
    public static string ResolveInteractionImagePath(Pet pet, string type)
    {
        ArgumentNullException.ThrowIfNull(pet);
        ArgumentNullException.ThrowIfNull(type);
        return Path.Combine(pet.SkinFolderPath, FileNamePrefix + type + FileExtension);
    }

    /// <summary><c>interaction_types.json</c> 的檔案結構（§6.5.2）。</summary>
    private sealed class InteractionTypesFile
    {
        [JsonPropertyName("types")]
        public List<string> Types { get; set; } = new();
    }
}
