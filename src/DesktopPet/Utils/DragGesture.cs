namespace DesktopPet.Utils;

/// <summary>
/// 拖曳／點擊判定的純運算（設計檔 §2.1「滑鼠點擊反應」／「拖曳功能」）。
/// </summary>
/// <remarks>
/// 設計檔未規定「多少位移才算拖曳」，此為 D3（輸入）任務的 UX 決策：對齊 Windows 系統慣例
/// （<c>SM_CXDRAG</c>/<c>SM_CYDRAG</c> 預設 4px），避免手抖被誤判為拖曳而吃掉本該觸發的
/// <c>CLICK</c> 事件視覺（§7.3.2）。
/// <para>
/// <b>用法（見 <c>UI/MainWindow.xaml.cs</c>）：</b>以 WPF <c>Window.DragMove()</c> 執行實際拖曳，
/// 讓 Windows 原生處理跨螢幕／不同 DPI 監視器的座標換算；<c>DragMove()</c> 返回後，把移動前後的
/// <c>Window.Left</c>/<c>Top</c>（同一組單位，無需再換算 DPI）差值交本類別純判定是否構成拖曳，
/// <c>false</c> 則視為一次點擊。純函式，不依賴 WPF，可跨平台單元測試。
/// </para>
/// </remarks>
public static class DragGesture
{
    /// <summary>判定閾值（與 <c>Window.Left</c>/<c>Top</c> 同單位）：位移小於此值視為點擊而非拖曳。</summary>
    public const double ClickDistanceThreshold = 4.0;

    /// <summary>
    /// 依總位移（Δx, Δy）判定是否構成拖曳：距離 <c>&gt;= threshold</c> 才算拖曳（恰好等於閾值也算，
    /// 寧可保守判定為拖曳也不誤觸事件動畫）。以平方距離比較，避免不必要的開方運算。
    /// </summary>
    public static bool IsDrag(double deltaX, double deltaY, double threshold = ClickDistanceThreshold)
    {
        double distanceSquared = deltaX * deltaX + deltaY * deltaY;
        return distanceSquared >= threshold * threshold;
    }
}
