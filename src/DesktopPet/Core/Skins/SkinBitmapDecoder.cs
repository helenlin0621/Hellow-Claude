using System;
using System.Windows.Media.Imaging;

namespace DesktopPet.Core.Skins;

/// <summary>
/// 把圖片檔解碼為凍結的 <see cref="BitmapSource"/>（設計檔 §7.3.6）。整張底圖（含 Sprite Sheet 全圖）
/// 只解碼一次，播放時切格只換矩形座標（見 §6.4.4 <see cref="FrameRef"/>），不重新配置記憶體。
/// </summary>
internal static class SkinBitmapDecoder
{
    /// <summary>
    /// 從絕對路徑解碼一張點陣圖。<c>OnLoad</c> 讓檔案在載入後即釋放（不鎖檔），
    /// <c>Freeze</c> 使其不可變、可跨執行緒共用並降低 WPF 開銷。
    /// </summary>
    public static BitmapSource Decode(string imagePath)
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource = new Uri(imagePath, UriKind.Absolute);
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.CreateOptions = BitmapCreateOptions.None;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }
}
