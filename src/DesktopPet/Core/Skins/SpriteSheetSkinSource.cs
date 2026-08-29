using System;
using System.Windows;
using System.Windows.Media.Imaging;
using DesktopPet.Core.Visuals;

namespace DesktopPet.Core.Skins;

/// <summary>
/// 多格（Sprite Sheet）素材來源（設計檔 §6.4.4 / §7.3.5）：依 <c>elapsed</c> 推算當前格號，
/// 回傳「整張底圖 + 該格矩形」。<c>loop</c> 為 <c>true</c> 時循環、<c>false</c> 時播完停在最後一格。
/// </summary>
/// <remarks>
/// 底圖只解碼一次（§7.3.6），切格只是換矩形座標，不重新配置記憶體。Sprite Sheet 一律單列橫向：
/// 每格寬 = <see cref="VisualUnitInfo.FrameWidth"/>、每格高 = 底圖高度（§6.4.2.1）。
/// 推格數學抽於 <see cref="SpriteSheetFrameMath"/>（純函式，跨平台可測）。
/// </remarks>
public sealed class SpriteSheetSkinSource : SkinSourceBase
{
    /// <inheritdoc cref="SkinSourceBase(VisualUnitInfo, string, LruFrameCache{BitmapSource}, Func{string, BitmapSource})"/>
    public SpriteSheetSkinSource(
        VisualUnitInfo unit,
        string imagePath,
        LruFrameCache<BitmapSource> cache,
        Func<string, BitmapSource>? decoder = null)
        : base(unit, imagePath, cache, decoder)
    {
    }

    /// <inheritdoc/>
    public override FrameRef GetFrame(PetVisualState state, TimeSpan elapsed)
    {
        var bmp = LoadBitmap();

        int index = SpriteSheetFrameMath.FrameIndex(elapsed, Unit.Fps ?? 0, Unit.Frames, Unit.Loops);

        // 格寬以 skin.json 明確指定者為準（§6.4.2.1「不以圖片尺寸推導格數」）；
        // 僅在描述檔異常缺 frameWidth 時，退而由底圖寬 / 格數推得，避免整組錯位。
        int frameWidth = Unit.FrameWidth ?? (Unit.Frames > 0 ? bmp.PixelWidth / Unit.Frames : bmp.PixelWidth);

        return new FrameRef
        {
            Source = bmp,
            Rect = new Int32Rect(index * frameWidth, 0, frameWidth, bmp.PixelHeight),
        };
    }
}
