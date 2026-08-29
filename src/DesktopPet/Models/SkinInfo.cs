namespace DesktopPet.Models;

/// <summary>
/// 已登記的一套圖樣資訊（設計檔 §5.1）。一套圖樣 = 一個資料夾（內含 anim_*.png 與 skin.json，
/// 見 §6.4 / §7.3.3）。內建與自訂圖樣共用同一結構，差別僅在 <see cref="SourceType"/>。
/// </summary>
public class SkinInfo
{
    public string Id { get; set; } = string.Empty;          // 圖樣唯一 ID（即資料夾名）
    public string Name { get; set; } = string.Empty;        // 顯示名稱
    public string SourceType { get; set; } = string.Empty;  // "builtin" / "custom"
    public string FolderPath { get; set; } = string.Empty;  // 圖樣資料夾路徑
    public DateTime ImportedDate { get; set; }              // 匯入日期（custom 用）
}
