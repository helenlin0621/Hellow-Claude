using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
// WPF 專案（UseWPF）的隱式 using 會帶入 System.Windows.Shapes.Path，與 System.IO.Path 撞名；
// 用別名把 Path 固定為 System.IO.Path，避免 CS0104 歧義（與 Utils/StorageManager 同慣例，勿移除）。
using Path = System.IO.Path;

namespace DesktopPet.Core.Skins;

/// <summary>
/// 一套圖樣資料夾內 <c>skin.json</c> 的記憶體模型與讀寫（設計檔 §6.4.5）。
/// </summary>
/// <remarks>
/// <b>兩條關鍵規則（§6.4.5，寫錯會讓 Sprite Sheet 凍結在第一格或既有素材無法沿用）：</b>
/// <list type="number">
///   <item><description><b>使用者永遠不需手寫此檔</b>——由匯入流程（§6.4.2.1）自動產生／維護。
///     本類別提供 <see cref="Save"/> 供匯入流程使用。</description></item>
///   <item><description><b>缺少 <c>skin.json</c> 的資料夾，一律視為所有單元皆 <c>frames: 1</c></b>——
///     既有素材與手動整理的資料夾零遷移即可使用。實作方式：<see cref="Load"/> 在檔案不存在／
///     無法解析時回傳「空 manifest」，而 <see cref="GetUnit"/> 對任何未登記的單元一律回傳
///     <c>frames: 1</c> 預設，兩者疊加即涵蓋「整份缺檔」與「單一單元未登記」兩種情況。</description></item>
/// </list>
/// 本類別不依賴 WPF（純 JSON + 檔案 IO），可跨平台單元測試。
/// </remarks>
public sealed class SkinManifest
{
    /// <summary><c>skin.json</c> 目前的格式版本（§6.4.5 表格）。</summary>
    public const int CurrentSchemaVersion = 2;

    /// <summary>圖樣資料夾內描述檔的固定檔名。</summary>
    public const string FileName = "skin.json";

    /// <summary>格式版本。</summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>
    /// 各單元的格數描述，鍵為單元名（例：<c>anim_idle_1</c>）。
    /// 未列於此的單元由 <see cref="GetUnit"/> 補上 <c>frames: 1</c> 預設。
    /// </summary>
    public Dictionary<string, VisualUnitInfo> Units { get; set; } = new();

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,   // schemaVersion / frameWidth …
        PropertyNameCaseInsensitive = true,                  // 容忍手動整理過的檔案大小寫
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, // 靜態圖只留 { "frames": 1 }
        WriteIndented = true,
    };

    /// <summary>
    /// 讀取指定圖樣資料夾內的 <c>skin.json</c>。檔案不存在、內容為空或無法解析時，
    /// 一律回傳空 manifest（§6.4.5：全視為單格），<b>不丟例外</b>——破損的描述檔不應讓寵物崩潰。
    /// 讀入後對每個單元正規化：填入 <see cref="VisualUnitInfo.Name"/>、將 <see cref="VisualUnitInfo.Frames"/>
    /// 夾為至少 1。
    /// </summary>
    /// <param name="skinFolderPath">圖樣資料夾路徑（內含 <c>anim_*.png</c> 與可選的 <c>skin.json</c>）。</param>
    public static SkinManifest Load(string skinFolderPath)
    {
        var manifest = new SkinManifest();
        if (string.IsNullOrWhiteSpace(skinFolderPath))
            return manifest;

        var path = Path.Combine(skinFolderPath, FileName);
        if (!File.Exists(path))
            return manifest;

        try
        {
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
                return manifest;

            var parsed = JsonSerializer.Deserialize<SkinManifest>(json, SerializerOptions);
            if (parsed is null)
                return manifest;

            parsed.Units ??= new(); // 容忍 "units": null（覆寫了預設初始化）
            parsed.Normalize();
            return parsed;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // 破損／無法讀取 → 退回「全視為單格」，維持零遷移的低門檻設計。
            return manifest;
        }
    }

    /// <summary>
    /// 取得單元的格數描述。已登記者回傳其描述；<b>未登記者一律回傳 <c>frames: 1</c> 預設</b>
    /// （§6.4.5：缺 <c>skin.json</c> 或單元未列入皆視為單格）。回傳物件的
    /// <see cref="VisualUnitInfo.Name"/> 必為傳入的 <paramref name="unitName"/>。
    /// </summary>
    public VisualUnitInfo GetUnit(string unitName)
    {
        if (Units.TryGetValue(unitName, out var info))
        {
            info.Name = unitName; // 確保鍵與 Name 一致（手動編輯或未經 Load 建構時的保險）
            return info;
        }

        return new VisualUnitInfo { Name = unitName, Frames = 1 };
    }

    /// <summary>
    /// 將本 manifest 寫入指定資料夾的 <c>skin.json</c>（供匯入流程使用，§6.4.2.1）。
    /// 靜態單元只輸出 <c>{ "frames": 1 }</c>（其餘欄位為 <c>null</c> 時省略）。
    /// </summary>
    public void Save(string skinFolderPath)
    {
        Directory.CreateDirectory(skinFolderPath);
        var path = Path.Combine(skinFolderPath, FileName);
        File.WriteAllText(path, JsonSerializer.Serialize(this, SerializerOptions));
    }

    /// <summary>把單元鍵回填到各 <see cref="VisualUnitInfo.Name"/>，並將格數夾為至少 1。</summary>
    private void Normalize()
    {
        foreach (var (name, info) in Units)
        {
            info.Name = name;
            if (info.Frames < 1) info.Frames = 1;
        }
    }
}
