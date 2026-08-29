using System.Windows;
using System.Windows.Interop;
using DesktopPet.Utils;

namespace DesktopPet.UI;

/// <summary>
/// 單隻寵物的透明置頂視窗（設計檔 §6.1 / §10.2 / §10.3）。
///
/// 每隻寵物各自一個此視窗實例（§6.1 多寵物模式）；由 E1 的 <c>PetInstance</c> 建立、
/// E2 的 <c>PetCoordinator</c> 管理，故 <c>App.xaml</c> 沒有 <c>StartupUri</c>。
/// </summary>
/// <remarks>
/// D1 只負責「視窗本身的行為」，把 WPF 沒有現成 API 的部分（§10.2）補齊：
/// <list type="bullet">
///   <item><b>WS_EX_TOOLWINDOW</b>：退出 Alt+Tab 與工作列，貼合常駐桌面寵物語意
///     （透明所需的 <c>WS_EX_LAYERED</c> 由 <c>AllowsTransparency=True</c> 自動掛上）。</item>
///   <item><b>防最小化消失</b>：攔 <c>WM_SYSCOMMAND/SC_MINIMIZE</c>，並在被「顯示桌面」等操作
///     壓成最小化時自動還原，確保寵物始終可見。</item>
///   <item><b>多監視器落點</b>：取視窗所在監視器的工作區（已排除工作列），以 DPI 轉為 DIU 後
///     交 <see cref="WindowPositioning"/> 夾制／擺放，達成「避免遮擋工作列」。</item>
/// </list>
/// XAML 的透明／無邊框／置頂等純宣告式屬性見 <c>MainWindow.xaml</c>；DPI（Per-Monitor V2）見
/// <c>app.manifest</c>。
/// <para>
/// <b>後續任務銜接：</b>點穿（<c>WS_EX_TRANSPARENT</c> 切換，連動 <c>Settings.ClickThrough</c>）屬 D2；
/// 拖曳／點擊等輸入屬 D3；把 <c>FrameRef</c> 畫到 <see cref="PetImage"/> 的素材渲染屬 D4。皆不在此處。
/// </para>
/// </remarks>
public partial class MainWindow : Window
{
    private HwndSource? _hwndSource;

    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 視窗控制代碼（HWND）就緒後套用延伸樣式、掛上訊息攔截、決定初始落點。
    /// 需在此而非建構式：HWND 要等視窗來源初始化後才存在。
    /// </summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _hwndSource = (HwndSource)PresentationSource.FromVisual(this)!;
        IntPtr hWnd = _hwndSource.Handle;

        // 退出 Alt+Tab／工作列（WS_EX_LAYERED 已由 AllowsTransparency 掛上）。
        NativeMethods.AddWindowExStyle(hWnd, NativeMethods.WS_EX_TOOLWINDOW);

        // 攔截系統命令以防最小化（§10.2）。
        _hwndSource.AddHook(WndProc);

        PlaceWithinCurrentMonitor();
    }

    protected override void OnClosed(EventArgs e)
    {
        _hwndSource?.RemoveHook(WndProc);
        _hwndSource = null;
        base.OnClosed(e);
    }

    /// <summary>
    /// 安全網（§10.2「防止被最小化時消失」）：即使有其他途徑（如「顯示桌面」Win+D）把視窗壓成
    /// 最小化，也立即還原。主要攔截在 <see cref="WndProc"/> 的 <c>SC_MINIMIZE</c>；此處補漏。
    /// </summary>
    protected override void OnStateChanged(EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        base.OnStateChanged(e);
    }

    /// <summary>攔截 <c>WM_SYSCOMMAND/SC_MINIMIZE</c>，直接吃掉最小化命令（§10.2）。</summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_SYSCOMMAND &&
            (wParam.ToInt64() & NativeMethods.SC_MASK) == NativeMethods.SC_MINIMIZE)
        {
            handled = true;   // 不轉給 DefWindowProc → 不最小化
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// 把視窗擺到所在監視器工作區的預設落點（§6.1 右下角、§10.2 不遮工作列）。
    /// 取不到工作區（極少數失敗）時退回 WPF 主螢幕工作區，仍保證不壓工作列。
    /// </summary>
    private void PlaceWithinCurrentMonitor()
    {
        RectD workArea = GetCurrentMonitorWorkAreaInDiu();
        RectD placed = WindowPositioning.DefaultPlacement(workArea, Width, Height);
        Left = placed.Left;
        Top = placed.Top;
    }

    /// <summary>
    /// 取當前監視器工作區並換算為 WPF 裝置無關單位（DIU）。<c>Left</c>/<c>Top</c>/<c>Width</c>/<c>Height</c>
    /// 皆為 DIU，故落點計算須在 DIU 空間進行——Per-Monitor V2 下各螢幕縮放不同，實體像素不可直接當座標。
    /// </summary>
    private RectD GetCurrentMonitorWorkAreaInDiu()
    {
        if (_hwndSource is not null &&
            NativeMethods.TryGetWorkArea(_hwndSource.Handle, out NativeMethods.RECT work))
        {
            // 實體像素 → DIU：以視窗來源的裝置轉換矩陣（含當前螢幕 DPI 縮放）換算。
            var fromDevice = _hwndSource.CompositionTarget.TransformFromDevice;
            double scaleX = fromDevice.M11;
            double scaleY = fromDevice.M22;
            return new RectD(
                work.Left * scaleX,
                work.Top * scaleY,
                work.Width * scaleX,
                work.Height * scaleY);
        }

        // 後備：WPF 主螢幕工作區（已是 DIU、已排除工作列）。
        var fallback = SystemParameters.WorkArea;
        return new RectD(fallback.Left, fallback.Top, fallback.Width, fallback.Height);
    }
}
