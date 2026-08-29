namespace DesktopPet.Utils;

/// <summary>
/// 矩形（左上角 + 寬高）。單位不限定：可為實體像素（Win32 工作區）或 WPF 裝置無關單位（DIU）。
/// 純資料型別，不依賴 WPF，供 <see cref="WindowPositioning"/> 與跨平台單元測試共用。
/// </summary>
public readonly record struct RectD(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;
}

/// <summary>
/// 視窗落點的純幾何計算（設計檔 §6.1「初始大小 / 位置」、§10.2「避免遮擋工作列」＋多監視器）。
/// </summary>
/// <remarks>
/// <b>職責分工：</b>本類別只做「矩形對矩形」的<b>純運算</b>，不碰 WPF、不做 P/Invoke、不管 DPI 換算。
/// 呼叫端（<c>UI/MainWindow.xaml.cs</c>）負責：以 <c>MonitorFromWindow</c>/<c>GetMonitorInfo</c> 取得
/// 當前監視器的工作區（實體像素、已排除工作列），再依視窗 DPI 轉為 DIU 後交給本類別。因此本類別
/// 可在 Linux/macOS 上單元測試（見 <c>WindowPositioningTests</c>）。
/// <para>
/// <b>為何吃「工作區」而非「螢幕全區」：</b>工作區（<c>rcWork</c>）本就排除工作列與已停靠的工具列，
/// 只要整個視窗落在工作區內即滿足 §10.2「避免遮擋工作列」；不需要另外知道工作列在哪一邊。
/// </para>
/// </remarks>
public static class WindowPositioning
{
    /// <summary>
    /// 把視窗矩形夾進工作區：超出的部分往內推，直到四邊都落在工作區內（§10.2）。
    /// 視窗比工作區大時（極小螢幕）以左上角對齊，至少保證左上角可見（可見度優先）。
    /// 只調整位置、不縮放尺寸。
    /// </summary>
    /// <param name="window">目標視窗矩形（與 <paramref name="workArea"/> 同單位）。</param>
    /// <param name="workArea">當前監視器的工作區（已排除工作列）。</param>
    public static RectD ClampToWorkArea(RectD window, RectD workArea)
    {
        double left = ClampAxis(window.Left, window.Width, workArea.Left, workArea.Width);
        double top = ClampAxis(window.Top, window.Height, workArea.Top, workArea.Height);
        return window with { Left = left, Top = top };
    }

    /// <summary>
    /// 新寵物的預設落點（§6.1）：工作區右下角、留邊距——桌面寵物慣常待著、且不壓到工作列的位置。
    /// 回傳的矩形已保證落在工作區內（極小螢幕時退回 <see cref="ClampToWorkArea"/> 的左上對齊）。
    /// </summary>
    /// <param name="workArea">當前監視器的工作區（已排除工作列）。</param>
    /// <param name="width">視窗寬（與 <paramref name="workArea"/> 同單位）。</param>
    /// <param name="height">視窗高。</param>
    /// <param name="margin">與工作區邊界的邊距，預設 24。</param>
    public static RectD DefaultPlacement(RectD workArea, double width, double height, double margin = 24)
    {
        double left = workArea.Right - width - margin;
        double top = workArea.Bottom - height - margin;
        return ClampToWorkArea(new RectD(left, top, width, height), workArea);
    }

    /// <summary>單一軸向的夾制：視窗不小於工作區時對齊起點，否則把座標夾在 [起點, 起點+可用長度] 內。</summary>
    private static double ClampAxis(double pos, double size, double areaStart, double areaSize)
    {
        if (size >= areaSize)
            return areaStart;                        // 視窗比工作區大：對齊起點（露出左上角）

        double max = areaStart + areaSize - size;    // 讓視窗右／下邊界最多貼齊工作區邊界
        return Math.Clamp(pos, areaStart, max);
    }
}
