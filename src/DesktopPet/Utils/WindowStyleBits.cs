namespace DesktopPet.Utils;

/// <summary>
/// 視窗樣式位元的純運算（設計檔 §10.2）。把「在既有樣式位元遮罩上設定／清除某個旗標」抽成不依賴
/// WPF／Win32 的純函式，供 <see cref="NativeMethods"/> 的讀改寫流程與跨平台單元測試共用。
/// </summary>
/// <remarks>
/// 讀改寫（read-modify-write）延伸視窗樣式時，<b>只能動目標旗標那幾個 bit、其餘一律保留</b>——
/// 例如開點穿（<c>WS_EX_TRANSPARENT</c>）不可清掉透明所需的 <c>WS_EX_LAYERED</c> 或 D1 掛上的
/// <c>WS_EX_TOOLWINDOW</c>。本函式即封裝這個「只翻指定 bit」的不變量，並以測試釘住。
/// </remarks>
public static class WindowStyleBits
{
    /// <summary>
    /// 在 <paramref name="current"/> 樣式遮罩上設定或清除 <paramref name="flag"/>，其餘位元保留。
    /// 冪等：重複套用同一 <paramref name="enabled"/> 結果不變。
    /// </summary>
    /// <param name="current">目前的延伸樣式遮罩（<c>GetWindowLongPtr(GWL_EXSTYLE)</c>）。</param>
    /// <param name="flag">要設定／清除的旗標位元（如 <c>WS_EX_TRANSPARENT</c>）。</param>
    /// <param name="enabled"><c>true</c> 設定該 bit；<c>false</c> 清除該 bit。</param>
    public static long Apply(long current, int flag, bool enabled)
    {
        long mask = flag & 0xFFFFFFFFL;   // 以 32 位元無號語意看待旗標，避免高位符號延伸誤清其他 bit
        return enabled ? current | mask : current & ~mask;
    }
}
