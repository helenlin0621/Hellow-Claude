using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;
using DesktopPet.Core.Visuals;

namespace DesktopPet.Core.Skins;

/// <summary>
/// 單一動畫單元的素材來源共用基底（設計檔 §6.4.4 / §7.3.6）。
/// </summary>
/// <remarks>
/// <b>粒度說明：</b>一個來源實例對應<b>一個動畫單元</b>（一個 <c>anim_*.png</c> 檔），是
/// <see cref="IPetSkinSource"/> 的葉節點實作。<c>GetFrame</c> 的 <see cref="PetVisualState"/> 參數
/// 對葉節點無作用（單元早已由 <c>PetVisualSelector</c>（B5）決定），保留僅為符合介面；
/// 未來由狀態→單元的組合層負責用到它。靜態圖與 Sprite Sheet 的差異以子類多型表達
/// （<see cref="StaticImageSkinSource"/> / <see cref="SpriteSheetSkinSource"/>），
/// <b>不用 <c>if (isSpriteSheet)</c> 分支</b>；建立哪個子類集中於 <see cref="SkinSourceFactory"/> 一處。
/// <para>
/// 底圖採延遲載入 + LRU（§7.3.6）：首次 <see cref="GetFrame"/> 才透過共用的
/// <see cref="LruFrameCache{TValue}"/> 解碼，之後命中快取。快取由整隻寵物共用（跨所有單元計 48 格）。
/// </para>
/// </remarks>
public abstract class SkinSourceBase : IPetSkinSource
{
    private readonly string _imagePath;
    private readonly LruFrameCache<BitmapSource> _cache;
    private readonly Func<string, BitmapSource> _decode;

    /// <summary>此來源對應的動畫單元描述（格數／格寬／fps／loop）。</summary>
    protected VisualUnitInfo Unit { get; }

    /// <param name="unit">單元描述（來自 <see cref="SkinManifest"/>）。</param>
    /// <param name="imagePath">該單元圖片的絕對路徑（同時作為快取鍵，於一隻寵物內唯一）。</param>
    /// <param name="cache">整隻寵物共用的格數 LRU 快取。</param>
    /// <param name="decoder">解碼委派（預設 <see cref="SkinBitmapDecoder.Decode"/>；可注入以利 Windows 端測試）。</param>
    protected SkinSourceBase(
        VisualUnitInfo unit,
        string imagePath,
        LruFrameCache<BitmapSource> cache,
        Func<string, BitmapSource>? decoder = null)
    {
        Unit = unit ?? throw new ArgumentNullException(nameof(unit));
        _imagePath = imagePath ?? throw new ArgumentNullException(nameof(imagePath));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _decode = decoder ?? SkinBitmapDecoder.Decode;
    }

    /// <summary>取得此單元的底圖（延遲載入；以整個單元的格數為權重放入 LRU）。</summary>
    protected BitmapSource LoadBitmap() => _cache.Get(_imagePath, Unit.Frames, () => _decode(_imagePath));

    /// <inheritdoc/>
    public abstract FrameRef GetFrame(PetVisualState state, TimeSpan elapsed);

    /// <inheritdoc/>
    /// <remarks>葉節點只承載自身單元；某狀態的完整單元清單（面板完成度）由上層登記表彙總。</remarks>
    public IReadOnlyList<VisualUnitInfo> GetUnits(PetVisualState state) => new[] { Unit };
}
