using System.IO;
using System.Windows.Media.Imaging;
using DesktopPet.Core.Skins;
using DesktopPet.Core.Visuals;
// WPF 專案（UseWPF）的隱式 using 會帶入 System.Windows.Shapes.Path，與 System.IO.Path 撞名；
// 用別名固定為 System.IO.Path（與 Utils/StorageManager 同慣例，勿移除）。
using Path = System.IO.Path;

namespace DesktopPet.Core;

/// <summary>一次 <see cref="AnimationManager.Tick"/> 的產出，供 WPF 端（<c>UI/MainWindow.xaml.cs</c>）繪製與排程下一次 tick。</summary>
public readonly struct AnimationFrame
{
    /// <summary>現在該畫的畫面；<c>null</c> 代表無可用素材（正常情況不會發生，NEUTRAL 為必填，§7.3.4）。</summary>
    public FrameRef? Frame { get; init; }

    /// <summary>下一步渲染計畫（§7.1.1：是否週期重繪、間隔多少）。</summary>
    public RenderPlan Plan { get; init; }

    /// <summary>本次實際顯示的狀態（事件或心情，供除錯／測試觀察）。</summary>
    public PetVisualState State { get; init; }
}

/// <summary>
/// D4 渲染綁定的核心邏輯（設計檔 §6.4.4 / §7.3.2）：串起 B 群已完成的視覺管線
/// （<see cref="VisualRegistry"/> → <see cref="PetVisualSelector"/> → <see cref="SkinManifest"/> /
/// <see cref="SkinSourceFactory"/> → <see cref="FrameRef"/>）與心情／事件的優先權判定
/// （<see cref="PetEventPriority"/>），每次 <see cref="Tick"/> 產出「現在該畫哪一格、多久後該再畫一次」。
/// </summary>
/// <remarks>
/// <b>職責邊界：</b>本類別<b>不</b>擁有 <c>DispatcherTimer</c>，也<b>不</b>直接碰 <c>Image</c> 控制項——
/// 那是 WPF 端的「綁定」動作，交給 <c>UI/MainWindow.xaml.cs</c>（<c>LoadSkin</c>/<c>AdvanceAndPaint</c>）：
/// 呼叫端依 <see cref="AnimationFrame.Plan"/> 設定計時器間隔，並把 <see cref="AnimationFrame.Frame"/>
/// 畫到 <c>Image.Source</c>（建議 <c>CroppedBitmap</c>，見 <see cref="FrameRef"/> 註解）。
/// <para>
/// <b>不擁有的部分（交由呼叫端／未來任務決定）：</b>
/// <list type="bullet">
///   <item><description><see cref="SetMood"/> 預設 <see cref="PetVisualState.Neutral"/>，需由狀態層
///     （C1 的 <c>StateManager</c> + <c>MoodEvaluator</c>）驅動，本類別不讀 <c>Pet.Hunger</c>/<c>Energy</c>。</description></item>
///   <item><description><c>pet_visuals.json</c> 只該讀一次：由呼叫端（未來的 <c>PetCoordinator</c>，E2）
///     載入後以 <see cref="VisualRegistry"/> 傳入，雙寵物共用同一份型別定義；各自獨立的只有
///     <see cref="LruFrameCache{TValue}"/>（§7.3.6：雙寵物快取互不共用）與 <see cref="PetVisualSelector"/>。</description></item>
///   <item><description>SLEEP「持續至醒來」的結束條件（例如 Energy 回滿）不在本類別判定，
///     需外部呼叫 <see cref="EndCurrentEvent"/>（見 <see cref="PetEventPriority"/> 註解）。</description></item>
/// </list>
/// </para>
/// 非執行緒安全（單一寵物 UI 執行緒），與 B 群其餘成員一致。
/// </remarks>
public sealed class AnimationManager
{
    private readonly string _skinFolderPath;
    private readonly VisualRegistry _registry;
    private readonly SkinManifest _manifest;
    private readonly PetVisualSelector _selector;
    private readonly LruFrameCache<BitmapSource> _cache;
    private readonly PetEventPriority _eventPriority;
    private readonly RenderTickController _renderTick;
    private readonly Func<DateTime> _now;
    private readonly Dictionary<string, IPetSkinSource> _sources = new();

    private PetVisualState _mood = PetVisualState.Neutral;
    private string? _lastUnitName;

    /// <param name="skinFolderPath">圖樣資料夾絕對路徑（內含 <c>anim_*.png</c> 與可選 <c>skin.json</c>，§7.3.3）。</param>
    /// <param name="registry">已載入的視覺類型登記表（§7.3.3；由呼叫端載入一次、多隻寵物可共用同一份）。</param>
    /// <param name="clock">時鐘（預設 <see cref="DateTime.UtcNow"/>）。內部各元件共用同一時鐘，避免互相漂移；可注入以利測試。</param>
    /// <param name="frameCacheCapacity">此隻寵物的 LRU 快取容量（格數，§7.3.6），預設 <see cref="LruFrameCache{TValue}.DefaultFrameCapacity"/>。</param>
    public AnimationManager(
        string skinFolderPath,
        VisualRegistry registry,
        Func<DateTime>? clock = null,
        int frameCacheCapacity = LruFrameCache<BitmapSource>.DefaultFrameCapacity)
    {
        _skinFolderPath = skinFolderPath ?? throw new ArgumentNullException(nameof(skinFolderPath));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _now = clock ?? (() => DateTime.UtcNow);

        _manifest = SkinManifest.Load(skinFolderPath);
        var pool = _registry.ScanUnits(skinFolderPath);
        _selector = new PetVisualSelector(pool, new VisualFallbackResolver(_registry), _now);
        _cache = new LruFrameCache<BitmapSource>(frameCacheCapacity);
        _eventPriority = new PetEventPriority(_now);
        _renderTick = new RenderTickController(_now);
    }

    /// <summary>更新當前心情（§7.2.1）。由狀態層驅動；僅 Neutral/Sad/LowEnergy 三值有意義（由呼叫端保證）。</summary>
    public void SetMood(PetVisualState mood) => _mood = mood;

    /// <summary>
    /// 觸發一個事件（§7.3.2，只接受 <see cref="PetVisualState.Click"/> /
    /// <see cref="PetVisualState.Feed"/> / <see cref="PetVisualState.Sleep"/>，由呼叫端保證）。
    /// 已有事件進行中時忽略（「進行中不被打斷」），連單元都不重抽、維持目前畫面；
    /// 缺素材且 fallback 為「不換圖」時同樣忽略。
    /// </summary>
    public void TriggerEvent(PetVisualState requestedEvent)
    {
        if (_eventPriority.HasActiveEvent)
            return; // 進行中不被打斷：連 ResolveUnit 都不呼叫，避免 selector 誤判成狀態改變而重抽。

        int rerollSec = _registry.GetDefinition(requestedEvent)?.RerollIntervalSec ?? 0;
        string? unitName = _selector.ResolveUnit(requestedEvent, rerollSec);
        if (unitName is null)
            return; // 缺素材、fallback 為「不換圖」：不進入事件狀態，維持目前畫面（§7.3.4）。

        double requiredSec = ComputeRequiredDurationSec(requestedEvent, unitName);
        _eventPriority.TryTrigger(requestedEvent, requiredSec);
    }

    /// <summary>
    /// 強制結束目前的持續型事件（睡眠「醒來」條件達成時由上層呼叫，見類別註解）。
    /// 無進行中事件、或進行中事件本就是限時型時為 no-op。
    /// </summary>
    public void EndCurrentEvent() => _eventPriority.EndEvent();

    /// <summary>
    /// 渲染 tick：由 WPF 端在計時器 tick、或 <see cref="SetMood"/>／<see cref="TriggerEvent"/> 之後立即呼叫。
    /// 決定現在該顯示的狀態（事件優先於心情，§7.3.2）、對應單元與畫面，以及下一步渲染計畫（§7.1.1）。
    /// </summary>
    public AnimationFrame Tick()
    {
        PetVisualState effective = _eventPriority.Resolve(_mood); // 內部先 Refresh：事件滿足最短秒數即結束、回到心情。

        int rerollSec = _registry.GetDefinition(effective)?.RerollIntervalSec ?? 0;
        string? unitName = _selector.ResolveUnit(effective, rerollSec);

        if (unitName is null)
            return new AnimationFrame { Frame = null, Plan = RenderPlan.Paused, State = effective };

        var unit = _manifest.GetUnit(unitName);

        if (unitName != _lastUnitName)
        {
            _lastUnitName = unitName;
            // 用 selector 記錄的實際單元起始時刻（而非另取一次「現在」），避免兩個元件的時間軸漂移。
            _renderTick.OnUnitChanged(unit, _now() - _selector.ElapsedInUnit);
        }

        var source = GetOrCreateSource(unitName, unit);
        var frame = source.GetFrame(effective, _selector.ElapsedInUnit);
        var plan = _renderTick.Evaluate();

        return new AnimationFrame { Frame = frame, Plan = plan, State = effective };
    }

    /// <summary>
    /// §7.3.2「至少 N 秒」：<c>max(durationSec, 動畫自然長度)</c>，自然長度 = <c>frames/fps</c>，
    /// <c>loop:true</c> 視為 0（由 <c>durationSec</c> 決定）。
    /// </summary>
    /// <remarks>
    /// <b>例外（§7.3.3 欄位說明）：</b>型別定義 <c>durationSec == 0</c>（目前僅 SLEEP）代表「持續型，
    /// 直到條件解除」，此語意優先於「至少 N 秒」公式——不論挑到的睡眠單元是否恰好是非循環動畫，
    /// 都不可被其短暫的自然長度提前結束（那會讓「持續至醒來」變成「播完幾秒就醒」）。
    /// 回傳 <c>0</c> 即代表此持續型語意，交給 <see cref="PetEventPriority"/> 不自動結束。
    /// </remarks>
    private double ComputeRequiredDurationSec(PetVisualState requestedEvent, string unitName)
    {
        double durationSec = _registry.GetDefinition(requestedEvent)?.DurationSec ?? 0;
        if (durationSec <= 0)
            return 0; // 持續型，不套用 max() 公式。

        var unit = _manifest.GetUnit(unitName);
        double naturalSec = unit.Loops || unit.Fps is not { } fps || fps <= 0
            ? 0
            : (double)unit.Frames / fps;

        return Math.Max(durationSec, naturalSec);
    }

    private IPetSkinSource GetOrCreateSource(string unitName, VisualUnitInfo unit)
    {
        if (_sources.TryGetValue(unitName, out var existing))
            return existing;

        var source = SkinSourceFactory.Create(unit, ResolveImagePath(unitName), _cache);
        _sources[unitName] = source;
        return source;
    }

    /// <summary>
    /// 由單元名還原實際圖片檔路徑：<see cref="VisualRegistry.ScanUnits"/> 只回傳去副檔名的單元名
    /// （供 selector 的單元池使用），副檔名需在此還原。嘗試的副檔名集合與 <see cref="VisualRegistry"/>
    /// 掃描時允許的一致（<see cref="VisualRegistry.AllowedExtensions"/>），避免兩處清單漂移。
    /// </summary>
    private string ResolveImagePath(string unitName)
    {
        foreach (var ext in VisualRegistry.AllowedExtensions)
        {
            var candidate = Path.Combine(_skinFolderPath, unitName + ext);
            if (File.Exists(candidate))
                return candidate;
        }

        // 理論上不會發生：unitName 必然來自 registry.ScanUnits() 對同一資料夾的掃描結果。
        throw new FileNotFoundException($"找不到單元 '{unitName}' 對應的圖片檔（{_skinFolderPath}）。", unitName);
    }
}
