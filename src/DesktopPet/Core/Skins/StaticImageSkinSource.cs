using System;
using System.Windows;
using System.Windows.Media.Imaging;
using DesktopPet.Core.Visuals;

namespace DesktopPet.Core.Skins;

/// <summary>
/// 單格（靜態圖）素材來源（設計檔 §6.4.4 / §6.4.5）：忽略 <c>elapsed</c>，永遠回傳整張圖。
/// 即「只有一格的動畫」——與 <see cref="SpriteSheetSkinSource"/> 走同一介面、無特例分支。
/// </summary>
public sealed class StaticImageSkinSource : SkinSourceBase
{
    /// <inheritdoc cref="SkinSourceBase(VisualUnitInfo, string, LruFrameCache{BitmapSource}, Func{string, BitmapSource})"/>
    public StaticImageSkinSource(
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
        return new FrameRef
        {
            Source = bmp,
            Rect = new Int32Rect(0, 0, bmp.PixelWidth, bmp.PixelHeight),
        };
    }
}
