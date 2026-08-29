using System.Windows;                 // Int32Rect
using System.Windows.Media.Imaging;   // BitmapSource

namespace DesktopPet.Core.Skins;

/// <summary>
/// 一格畫面的描述：「哪張底圖的哪個矩形」（設計檔 §6.4.4）。
/// </summary>
/// <remarks>
/// <b>為何是「底圖 + 矩形」而非每格新建 <c>BitmapImage</c>：</b>Sprite Sheet 每格若都 crop 成新
/// <c>BitmapImage</c>，播放時每秒會產生 12–15 個短命物件，GC 壓力大。改回傳整張底圖 + 矩形座標後，
/// 底圖只載入／解碼一次，切格只是換 <see cref="Rect"/>，WPF 端用 <c>CroppedBitmap</c> 或
/// <c>ImageBrush</c> 的 <c>Viewbox</c> 呈現，不重新配置記憶體（渲染綁定於 D4 接上）。
/// <para>
/// <b>靜態圖不是特例分支</b>：其 <see cref="Rect"/> 填整張圖的完整範圍、忽略 <c>elapsed</c>——
/// 即「只有一格的動畫」，呼應 §6.4.5 統一模型。上層只呼叫
/// <see cref="IPetSkinSource.GetFrame"/>，不需知道底層是單格或多格。
/// </para>
/// </remarks>
public readonly struct FrameRef
{
    /// <summary>底圖（整張 PNG；Sprite Sheet 時為含全部格數的整圖）。</summary>
    public BitmapSource Source { get; init; }

    /// <summary>要顯示的區域（靜態圖為整張範圍；Sprite Sheet 為當前格的矩形）。</summary>
    public Int32Rect Rect { get; init; }
}
