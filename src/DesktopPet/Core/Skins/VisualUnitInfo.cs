using System.Text.Json.Serialization;

namespace DesktopPet.Core.Skins;

/// <summary>
/// 單一「動畫單元」的描述（設計檔 §6.4.5 統一動畫單元模型）。
/// </summary>
/// <remarks>
/// <b>核心原則：靜態圖 = <c>Frames == 1</c> 的 Sprite Sheet。</b>全系統只有一套邏輯，
/// 不存在 <c>if (isSpriteSheet)</c> 分支——靜態圖只是「只有一格的動畫」。
/// <para>
/// 這同時是 <c>skin.json</c> 中 <c>units</c> 每個條目的反序列化目標，也是
/// <see cref="IPetSkinSource.GetUnits"/> 回傳給面板（§6.6.1 完成度）用的資訊物件。
/// <see cref="Name"/> 由單元鍵（例：<c>anim_idle_1</c>）填入，不寫進 <c>skin.json</c> 的值物件
/// （鍵即單元名，避免重複），故標 <see cref="JsonIgnoreAttribute"/>。
/// </para>
/// </remarks>
public sealed class VisualUnitInfo
{
    /// <summary>
    /// 單元名（檔名去副檔名，例：<c>anim_idle_1</c>）。由 <c>skin.json</c> 的鍵或掃描到的檔名填入，
    /// 不參與序列化（值物件不重複存名）。
    /// </summary>
    [JsonIgnore]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 格數。<c>1</c> 即靜態圖（其餘欄位可省略）；<c>&gt; 1</c> 為 Sprite Sheet。
    /// <see cref="SkinManifest.Load"/> 會保證此值 <b>至少為 1</b>（正規化非法值），
    /// 讓 B3 依 <c>elapsed</c> 推格時可安全對 <see cref="Frames"/> 取模。
    /// </summary>
    public int Frames { get; set; } = 1;

    /// <summary>每格寬度（px）。每格高度由圖片總高推得。僅 Sprite Sheet 有值（靜態圖為 <c>null</c>）。</summary>
    public int? FrameWidth { get; set; }

    /// <summary>播放速率（fps）。僅 Sprite Sheet 有值（靜態圖為 <c>null</c>）。桌寵建議 12–15 fps（§7.1.1）。</summary>
    public int? Fps { get; set; }

    /// <summary>
    /// 播完是否循環。<c>false</c> 時停在最後一格（§7.1.1）。靜態圖不適用，故為 <c>null</c>；
    /// 語意布林請用 <see cref="Loops"/> 取得（<c>null</c> 視為 <c>false</c>）。
    /// </summary>
    public bool? Loop { get; set; }

    /// <summary>是否為多格動畫（<c>Frames &gt; 1</c>）。靜態圖為 <c>false</c>。不序列化。</summary>
    [JsonIgnore]
    public bool IsAnimated => Frames > 1;

    /// <summary><see cref="Loop"/> 的語意布林：<c>null</c> 視為不循環。不序列化。</summary>
    [JsonIgnore]
    public bool Loops => Loop ?? false;
}
