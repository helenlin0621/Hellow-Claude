using System;
using System.Windows.Media.Imaging;

namespace DesktopPet.Core.Skins;

/// <summary>
/// 依單元格數建立對應的素材來源（設計檔 §6.4.5）。這是「靜態圖 vs Sprite Sheet」<b>唯一</b>的分支點：
/// 統一動畫單元模型要求全系統只有一處決定用哪個實作，其餘程式一律只看
/// <see cref="IPetSkinSource"/>，不再散落 <c>if (isSpriteSheet)</c>。
/// </summary>
public static class SkinSourceFactory
{
    /// <summary>
    /// <see cref="VisualUnitInfo.IsAnimated"/>（<c>Frames &gt; 1</c>）→ <see cref="SpriteSheetSkinSource"/>，
    /// 否則 → <see cref="StaticImageSkinSource"/>。
    /// </summary>
    /// <param name="unit">單元描述（來自 <see cref="SkinManifest"/>）。</param>
    /// <param name="imagePath">該單元圖片的絕對路徑。</param>
    /// <param name="cache">整隻寵物共用的格數 LRU 快取。</param>
    /// <param name="decoder">解碼委派（預設 <see cref="SkinBitmapDecoder.Decode"/>；可注入以利測試）。</param>
    public static IPetSkinSource Create(
        VisualUnitInfo unit,
        string imagePath,
        LruFrameCache<BitmapSource> cache,
        Func<string, BitmapSource>? decoder = null)
    {
        ArgumentNullException.ThrowIfNull(unit);

        return unit.IsAnimated
            ? new SpriteSheetSkinSource(unit, imagePath, cache, decoder)
            : new StaticImageSkinSource(unit, imagePath, cache, decoder);
    }
}
