using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DesktopPet.Core;
using DesktopPet.Core.Visuals;
using DesktopPet.Utils;

namespace DesktopPet.UI;

/// <summary>
/// 單隻寵物的透明置頂視窗（設計檔 §6.1 / §10.2 / §10.3）。
///
/// 每隻寵物各自一個此視窗實例（§6.1 多寵物模式）；由 E1 的 <c>PetInstance</c> 建立、
/// E2 的 <c>PetCoordinator</c> 管理，故 <c>App.xaml</c> 沒有 <c>StartupUri</c>。
/// </summary>
/// <remarks>
/// 本視窗（D1/D2/D3/D4）負責「視窗本身的行為」，把 WPF 沒有現成 API 的部分（§10.2）補齊：
/// <list type="bullet">
///   <item><b>WS_EX_TOOLWINDOW</b>：退出 Alt+Tab 與工作列，貼合常駐桌面寵物語意
///     （透明所需的 <c>WS_EX_LAYERED</c> 由 <c>AllowsTransparency=True</c> 自動掛上）。</item>
///   <item><b>防最小化消失</b>：攔 <c>WM_SYSCOMMAND/SC_MINIMIZE</c>，並在被「顯示桌面」等操作
///     壓成最小化時自動還原，確保寵物始終可見。</item>
///   <item><b>多監視器落點</b>：取視窗所在監視器的工作區（已排除工作列），以 DPI 轉為 DIU 後
///     交 <see cref="WindowPositioning"/> 夾制／擺放，達成「避免遮擋工作列」。</item>
///   <item><b>點穿模式</b>（D2，§2.1）：<see cref="ClickThrough"/> 切換 <c>WS_EX_TRANSPARENT</c>，
///     讓滑鼠事件穿透到底下視窗。連動 <c>Settings.ClickThrough</c>。</item>
///   <item><b>輸入 + 右鍵選單</b>（D3，§2.1/§6.3/§7.3.2）：左鍵區分點擊／拖曳／雙擊
///     （<see cref="OnMouseLeftButtonDown"/>）；右鍵選單 7 項（<c>MainWindow.xaml</c> 的
///     <c>Window.ContextMenu</c>）。本視窗只<b>負責偵測與觸發</b>，不處理下游效果（見
///     <see cref="EventTriggered"/> / <see cref="MenuActionRequested"/> / <see cref="DoubleClicked"/>
///     的個別註解）。</item>
///   <item><b>渲染綁定</b>（D4，§6.4.4/§7.3.2）：<see cref="LoadSkin"/> 接上
///     <c>Core/AnimationManager.cs</c>（B 群視覺管線 + 事件優先權），把它算出的
///     <see cref="AnimationFrame"/> 畫到 <see cref="PetImage"/>，並依其
///     <c>Plan</c>（§7.1.1 動態渲染頻率）調度 <see cref="DispatcherTimer"/>。D3 的點擊／
///     餵食／睡眠事件與 <see cref="SetMood"/> 皆會立即驅動一次重繪（見
///     <see cref="AdvanceAndPaint"/>），不必等計時器下次觸發。</item>
///   <item><b>互動素材顯示</b>（E3，§6.5.2）：<see cref="ShowInteraction"/>／
///     <see cref="ClearInteraction"/> 讓 <c>Core/PetCoordinator.cs</c> 直接畫上固定單張的
///     <c>interaction_*.png</c>，繞過事件優先權管線；<see cref="IsEventActive"/> 供其判斷本視窗
///     「是否閒置」（§6.5.4 greet 條件）。</item>
/// </list>
/// XAML 的透明／無邊框／置頂等純宣告式屬性見 <c>MainWindow.xaml</c>；DPI（Per-Monitor V2）見
/// <c>app.manifest</c>。
/// <para>
/// <b>後續任務銜接：</b>E1 的 <c>Core/PetInstance.cs</c> 已接上 <c>StateManager</c>／
/// <c>HappinessManager</c>／<c>MoodEvaluator</c> 驅動 <see cref="SetMood"/>、以
/// <c>Pet.SkinFolderPath</c> 呼叫 <see cref="LoadSkin"/>，並對 <see cref="EventTriggered"/>／
/// <see cref="MenuActionRequested"/> 施加通用互動記帳（歸零冷落計時、點擊/玩耍與餵食的幸福度回補）。
/// 仍未定案、留給 E4 的部分：餵食扣飢餓、睡眠回能量與 SLEEP「醒來」時呼叫
/// <c>AnimationManager.EndCurrentEvent</c>（本視窗尚未對外公開 <c>AnimationManager</c>，該任務需
/// 一併補上出口）、玩耍/清潔的實際數值效果、<c>Settings.ClickThrough</c> 的存讀整合；設置／關於
/// 視窗（Phase 2）尚未建立。皆不在此處。
/// </para>
/// </remarks>
public partial class MainWindow : Window
{
    private HwndSource? _hwndSource;
    private bool _clickThrough;
    private AnimationManager? _animation;
    private DispatcherTimer? _renderTimer;
    private bool _interactionActive;

    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 多寵物模式下的落點序號（§6.1/§6.5，由 E2 的 <c>PetCoordinator</c> 依建立順序指派，預設 0）。
    /// 只影響初始水平偏移，避免多隻寵物的預設落點完全疊在一起而看起來只有一隻——使用者仍可事後
    /// 各自拖曳到想要的位置（D3），本屬性只管「剛啟動看得見幾隻」。須在 <see cref="Show"/> 前設定。
    /// </summary>
    public int PlacementIndex { get; set; }

    /// <summary>
    /// 點穿模式（§2.1「點穿模式（允許點擊下方）」／§10.2）：<c>true</c> 時掛上 <c>WS_EX_TRANSPARENT</c>，
    /// 滑鼠點擊／拖曳穿透到桌面或底下視窗；<c>false</c> 時恢復可互動。連動 <c>Settings.ClickThrough</c>
    /// （由 E2/E4 於建立視窗時與設定變更時指派本屬性；預設 <c>false</c>）。
    /// </summary>
    /// <remarks>
    /// 在 HWND 就緒前設定亦安全：值先記錄，於 <see cref="OnSourceInitialized"/> 一併套用。
    /// 開點穿只翻 <c>WS_EX_TRANSPARENT</c> 一個 bit（見 <see cref="WindowStyleBits"/>），不影響透明
    /// （<c>WS_EX_LAYERED</c>）與退出 Alt+Tab（<c>WS_EX_TOOLWINDOW</c>）。
    /// <para>
    /// <b>注意（設計取捨）：</b>開啟後寵物本身也不再收滑鼠事件，故無法用「點寵物」關閉點穿；
    /// 需由其他入口切換（如 Phase 2 的系統托盤選單／快速鍵）。此屬於上層 UI，不在 D2 範圍。
    /// </para>
    /// </remarks>
    public bool ClickThrough
    {
        get => _clickThrough;
        set
        {
            _clickThrough = value;
            ApplyClickThrough();
        }
    }

    /// <summary>
    /// 視覺事件已觸發（§7.3.2）：一定是 <see cref="PetVisualState.Click"/> /
    /// <see cref="PetVisualState.Feed"/> / <see cref="PetVisualState.Sleep"/> 三者之一
    /// （沿用既有列舉，不另建平行型別）。由 D4 訂閱以決定播放哪個事件單元；本類別只判定
    /// 「該不該觸發」（點擊需先排除拖曳；餵食／睡眠來自右鍵選單），<b>不</b>處理事件優先權、
    /// 「至少 N 秒」持續時間、或「進行中不被打斷」（§7.3.2 那些規則屬 D4 的渲染邏輯）。
    /// </summary>
    public event EventHandler<PetVisualState>? EventTriggered;

    /// <summary>
    /// 雙擊（§2.1「雙擊特殊動作」）。設計檔僅列於功能清單，未定義具體效果——本任務只負責
    /// 偵測（左鍵 <c>ClickCount &gt;= 2</c>）並提供事件出口，語意留待後續任務決定。
    /// </summary>
    public event EventHandler? DoubleClicked;

    /// <summary>
    /// 右鍵選單中「無對應視覺事件」的指令（§6.3，見 <see cref="PetMenuAction"/> 註解：
    /// 餵食／睡眠改走 <see cref="EventTriggered"/>，不在此重複發送）。「退出」例外——見
    /// <see cref="OnExitMenuClick"/>，本視窗會直接處理其副作用，其餘 4 項只發事件不做事。
    /// </summary>
    public event EventHandler<PetMenuAction>? MenuActionRequested;

    // ── D4：渲染綁定（§6.4.4/§7.3.2）─────────────────────────────

    /// <summary>
    /// 載入圖樣並開始渲染。由呼叫端（尚未建立的 E1/E2）決定要載入哪一套圖樣、何時呼叫；
    /// 本視窗只負責「接到路徑後把畫面顯示出來並持續播放」。可重複呼叫以切換圖樣
    /// （例如使用者換主題）：會重建整條渲染管線（各自的 LRU 快取等），舊管線與計時器訂閱自然汰換。
    /// </summary>
    /// <param name="skinFolderPath">圖樣資料夾絕對路徑（§7.3.3）。</param>
    /// <param name="registry">已載入的視覺類型登記表（§7.3.3；通常由呼叫端載入 <c>pet_visuals.json</c>
    /// 一次，多隻寵物共用同一份）。</param>
    public void LoadSkin(string skinFolderPath, VisualRegistry registry)
    {
        _renderTimer?.Stop();
        _animation = new AnimationManager(skinFolderPath, registry);

        _renderTimer ??= new DispatcherTimer(DispatcherPriority.Render);
        _renderTimer.Tick -= OnRenderTimerTick; // 避免 LoadSkin 被再次呼叫時重複訂閱。
        _renderTimer.Tick += OnRenderTimerTick;

        AdvanceAndPaint();
    }

    /// <summary>更新當前心情（§7.2.1，由狀態層驅動）並立即反映。<see cref="LoadSkin"/> 尚未呼叫時為 no-op。</summary>
    public void SetMood(PetVisualState mood)
    {
        _animation?.SetMood(mood);
        AdvanceAndPaint();
    }

    /// <summary>
    /// 是否有事件（Click/Feed/Sleep）進行中（§7.3.2）。<see cref="LoadSkin"/> 尚未呼叫時視為
    /// <c>false</c>（無事件可言）。供 E3 互動系統（<c>Core/PetCoordinator.cs</c>）判斷「是否閒置」
    /// （§6.5.4 greet 觸發條件）。
    /// </summary>
    public bool IsEventActive => _animation?.HasActiveEvent ?? false;

    /// <summary>
    /// 顯示互動素材（§6.5.2）：固定單張靜態圖，直接畫到 <see cref="PetImage"/>，<b>繞過</b>
    /// <see cref="AnimationManager"/> 管線（互動素材不比照 §7.3 開放多張隨機／事件優先權，見
    /// 設計檔理由：兩隻寵物各自抽圖會不同步）。由 <c>Core/PetCoordinator.cs</c>（E3）依
    /// <c>PetInteractionChecker</c>／<c>InteractionRules</c> 的判定結果呼叫；暫停渲染計時器，
    /// 避免正常的心情／事件重繪蓋掉互動畫面，直到 <see cref="ClearInteraction"/> 被呼叫為止。
    /// </summary>
    /// <param name="imagePath">互動素材的絕對檔案路徑（<c>interaction_[類型].png</c>）。</param>
    public void ShowInteraction(string imagePath)
    {
        _renderTimer?.Stop();
        _interactionActive = true;
        PetImage.Source = new BitmapImage(new Uri(imagePath, UriKind.Absolute));
    }

    /// <summary>結束互動顯示，回到正常的心情／事件渲染。目前未在顯示互動時為 no-op。</summary>
    public void ClearInteraction()
    {
        if (!_interactionActive)
            return;

        _interactionActive = false;
        AdvanceAndPaint();
    }

    private void OnRenderTimerTick(object? sender, EventArgs e) => AdvanceAndPaint();

    /// <summary>
    /// 執行一次渲染 tick（<c>AnimationManager.Tick</c>）：把回傳的畫面畫到 <see cref="PetImage"/>，
    /// 並依 §7.1.1 的渲染計畫決定計時器該暫停還是以多快的間隔繼續（動態 1–15 Hz；靜態單元或
    /// 非循環動畫播完時暫停，直到下次心情變化或事件觸發再被本方法喚醒）。<see cref="ShowInteraction"/>
    /// 顯示中時整個 no-op（§6.5.2 互動畫面優先，見 <see cref="ShowInteraction"/> 註解）。
    /// </summary>
    private void AdvanceAndPaint()
    {
        if (_animation is null || _renderTimer is null || _interactionActive)
            return;

        var result = _animation.Tick();

        if (result.Frame is { } frame)
            PetImage.Source = new CroppedBitmap(frame.Source, frame.Rect);

        if (result.Plan.ShouldRedraw)
        {
            _renderTimer.Interval = result.Plan.Interval;
            _renderTimer.Start(); // 已在跑時為 no-op。
        }
        else
        {
            _renderTimer.Stop();
        }
    }

    /// <summary>
    /// D3 觸發點的共用出口：對外通知（<see cref="EventTriggered"/>）、驅動內部渲染
    /// （<see cref="AnimationManager.TriggerEvent"/>）、並立即重繪一次——不必等（可能已暫停的）
    /// 計時器下次觸發才反映使用者操作。
    /// </summary>
    private void RaiseEvent(PetVisualState state)
    {
        EventTriggered?.Invoke(this, state);
        _animation?.TriggerEvent(state);
        AdvanceAndPaint();
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
        NativeMethods.SetWindowExStyle(hWnd, NativeMethods.WS_EX_TOOLWINDOW, enabled: true);

        // 套用在 HWND 就緒前可能已設定的點穿狀態（§2.1）。
        ApplyClickThrough();

        // 攔截系統命令以防最小化（§10.2）。
        _hwndSource.AddHook(WndProc);

        PlaceWithinCurrentMonitor();
    }

    /// <summary>依 <see cref="_clickThrough"/> 切換 <c>WS_EX_TRANSPARENT</c>；HWND 未就緒時延到 <see cref="OnSourceInitialized"/>。</summary>
    private void ApplyClickThrough()
    {
        if (_hwndSource is null)
            return;

        NativeMethods.SetWindowExStyle(_hwndSource.Handle, NativeMethods.WS_EX_TRANSPARENT, _clickThrough);
    }

    protected override void OnClosed(EventArgs e)
    {
        _renderTimer?.Stop(); // DispatcherTimer 不會隨視窗關閉自動停止，需主動停止避免懸掛的渲染 tick。
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

    /// <summary>單一寵物落點的水平間距（§6.1/§6.5，<see cref="PlacementIndex"/> 每 +1 往左讓一個視窗寬）。</summary>
    private const double PlacementSlotMargin = 24;

    /// <summary>
    /// 把視窗擺到所在監視器工作區的預設落點（§6.1 右下角、§10.2 不遮工作列），
    /// 再依 <see cref="PlacementIndex"/> 往左偏移，讓多隻寵物的初始落點不完全重疊（§6.5）。
    /// 取不到工作區（極少數失敗）時退回 WPF 主螢幕工作區，仍保證不壓工作列。
    /// </summary>
    private void PlaceWithinCurrentMonitor()
    {
        RectD workArea = GetCurrentMonitorWorkAreaInDiu();
        RectD placed = WindowPositioning.DefaultPlacement(workArea, Width, Height);

        if (PlacementIndex > 0)
        {
            double left = placed.Left - PlacementIndex * (Width + PlacementSlotMargin);
            placed = WindowPositioning.ClampToWorkArea(placed with { Left = left }, workArea);
        }

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

    // ── D3：輸入（§2.1）─────────────────────────────────────────

    /// <summary>
    /// 滑鼠左鍵按下：先判斷雙擊（<c>ClickCount &gt;= 2</c>）直接觸發 <see cref="DoubleClicked"/>，
    /// 不啟動拖曳（避免第二擊些微位移被誤判為拖曳）。否則以 <see cref="DragMove"/> 交給 Windows
    /// 原生處理拖曳——好處是跨螢幕／不同 DPI 監視器時的座標換算由系統負責，不必像
    /// <see cref="PlaceWithinCurrentMonitor"/> 那樣手動轉換；<c>DragMove()</c> 為同步阻塞呼叫，
    /// 待放開滑鼠才返回。返回後比較移動前後的 <see cref="Left"/>/<see cref="Top"/>（同單位，
    /// 無需再換算 DPI），位移未達 <see cref="DragGesture.ClickDistanceThreshold"/> 視為點擊而非
    /// 拖曳，觸發 <c>CLICK</c> 事件（§7.3.2）。
    /// </summary>
    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2)
        {
            DoubleClicked?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        double startLeft = Left;
        double startTop = Top;

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // 極少數情況（按下瞬間已放開、擷取失敗）：視為未移動，走下方點擊判定。
        }

        if (!DragGesture.IsDrag(Left - startLeft, Top - startTop))
            RaiseEvent(PetVisualState.Click);

        e.Handled = true;
    }

    // ── D3：右鍵選單（§6.3）─────────────────────────────────────

    private void OnFeedMenuClick(object sender, RoutedEventArgs e) => RaiseEvent(PetVisualState.Feed);

    private void OnSleepMenuClick(object sender, RoutedEventArgs e) => RaiseEvent(PetVisualState.Sleep);

    private void OnPlayMenuClick(object sender, RoutedEventArgs e) =>
        MenuActionRequested?.Invoke(this, PetMenuAction.Play);

    private void OnCleanMenuClick(object sender, RoutedEventArgs e) =>
        MenuActionRequested?.Invoke(this, PetMenuAction.Clean);

    private void OnSettingsMenuClick(object sender, RoutedEventArgs e) =>
        MenuActionRequested?.Invoke(this, PetMenuAction.Settings);

    private void OnAboutMenuClick(object sender, RoutedEventArgs e) =>
        MenuActionRequested?.Invoke(this, PetMenuAction.About);

    /// <summary>
    /// 退出：選單 7 項中唯一由本視窗直接執行副作用者。其餘動作只發事件，效果交給尚未建立的
    /// 上層（E1/E2/E4）決定；「退出」不需依賴任何未完成元件即可有意義地動作，且
    /// <c>Application.Shutdown()</c> 會先觸發各視窗的 <c>Closing</c>／<c>Closed</c>——未來 E4
    /// 若要「關閉前存檔」（§8.2），掛那兩個事件即可，不需更動此處。
    /// </summary>
    private void OnExitMenuClick(object sender, RoutedEventArgs e)
    {
        MenuActionRequested?.Invoke(this, PetMenuAction.Exit);
        Application.Current.Shutdown();
    }
}
