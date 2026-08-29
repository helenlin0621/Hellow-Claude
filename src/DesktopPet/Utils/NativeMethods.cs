using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DesktopPet.Utils;

/// <summary>
/// Win32 互操作（P/Invoke）集中處（設計檔 §10.2）。WPF 沒有現成 API 可做「延伸視窗樣式調整」與
/// 「取當前監視器工作區」，一律在此以 P/Invoke 封裝，避免相關魔術數字散落各處。
/// </summary>
/// <remarks>
/// 本檔含視窗層（D1/D2）需要的最小集合：
/// <list type="bullet">
///   <item><b>設定／清除延伸視窗樣式</b>：D1 掛 <c>WS_EX_TOOLWINDOW</c>（不進 Alt+Tab／工作列）；
///     D2 切換 <c>WS_EX_TRANSPARENT</c>（點穿）。位元運算委由純函式 <see cref="WindowStyleBits.Apply"/>
///     以保「只翻目標 bit」。透明所需的 <c>WS_EX_LAYERED</c> 由 <c>AllowsTransparency=True</c> 自動掛上。</item>
///   <item><b>當前監視器工作區</b>（<c>MonitorFromWindow</c> + <c>GetMonitorInfo</c>）：多監視器下取「視窗所在那一個」
///     的工作區（已排除工作列），交給 <see cref="WindowPositioning"/> 夾制落點（§10.2）。</item>
/// </list>
/// 全類別限 Windows（<see cref="SupportedOSPlatformAttribute"/>），故不納入跨平台測試專案。
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class NativeMethods
{
    // ── 視窗訊息（WndProc 攔截用）─────────────────────────────────
    public const int WM_SYSCOMMAND = 0x0112;

    /// <summary>系統命令：最小化。攔下可避免寵物被最小化而消失（§10.2）。比對前需先以 <see cref="SC_MASK"/> 遮罩。</summary>
    public const int SC_MINIMIZE = 0xF020;

    /// <summary><c>WM_SYSCOMMAND</c> 的 <c>wParam</c> 低 4 bit 由系統保留，比對命令前需先遮罩。</summary>
    public const int SC_MASK = 0xFFF0;

    // ── 延伸視窗樣式 ─────────────────────────────────────────────
    public const int GWL_EXSTYLE = -20;

    /// <summary>工具視窗：不出現在 Alt+Tab 與工作列，貼合常駐桌面寵物語意（§6.1）。</summary>
    public const int WS_EX_TOOLWINDOW = 0x00000080;

    /// <summary>點穿：滑鼠事件穿透到底下視窗（§2.1「點穿模式」）。由 D2 依 <c>Settings.ClickThrough</c> 切換。</summary>
    public const int WS_EX_TRANSPARENT = 0x00000020;

    /// <summary>
    /// 在視窗的延伸樣式上設定（<paramref name="enabled"/>=true）或清除（false）一個旗標位元，其餘位元保留。
    /// 位元運算委由 <see cref="WindowStyleBits.Apply"/>（純函式、有測試），本方法只負責讀改寫 HWND。
    /// </summary>
    public static void SetWindowExStyle(IntPtr hWnd, int exStyle, bool enabled)
    {
        long current = GetWindowLongPtr(hWnd, GWL_EXSTYLE).ToInt64();
        long updated = WindowStyleBits.Apply(current, exStyle, enabled);
        SetWindowLongPtr(hWnd, GWL_EXSTYLE, new IntPtr(updated));
    }

    /// <summary>
    /// 取得視窗所在監視器的工作區（實體像素、已排除工作列，§10.2）。失敗時回傳 <c>false</c>。
    /// </summary>
    public static bool TryGetWorkArea(IntPtr hWnd, out RECT workArea)
    {
        workArea = default;
        IntPtr monitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero)
            return false;

        var info = MONITORINFO.Create();
        if (!GetMonitorInfo(monitor, ref info))
            return false;

        workArea = info.rcWork;
        return true;
    }

    // ── P/Invoke 宣告 ────────────────────────────────────────────

    // GetWindowLongPtr/SetWindowLongPtr 在 32 位元行程並不存在（只有無 Ptr 版），
    // 依 IntPtr.Size 選對進入點，讓 x86/x64/Arm64 皆正確。
    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        => IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        => IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
            : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    /// <summary>Win32 <c>RECT</c>（left/top/right/bottom，實體像素）。</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;

        public static MONITORINFO Create() => new() { cbSize = Marshal.SizeOf<MONITORINFO>() };
    }
}
