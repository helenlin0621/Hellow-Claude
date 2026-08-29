using DesktopPet.Core.Skins;

namespace DesktopPet.Core.Visuals;

/// <summary>
/// 一次渲染決策的結果（§7.1.1）：是否需要週期重繪，以及若需要則其間隔。
/// </summary>
public readonly struct RenderPlan : IEquatable<RenderPlan>
{
    /// <summary>是否需要週期重繪。<c>false</c> = 暫停重繪（靜態圖，或非循環動畫已播完）。</summary>
    public bool ShouldRedraw { get; private init; }

    /// <summary>需要重繪時的間隔（<see cref="ShouldRedraw"/> 為 <c>false</c> 時為 <see cref="TimeSpan.Zero"/>）。</summary>
    public TimeSpan Interval { get; private init; }

    /// <summary>暫停重繪。</summary>
    public static RenderPlan Paused => new() { ShouldRedraw = false, Interval = TimeSpan.Zero };

    /// <summary>以指定間隔週期重繪。</summary>
    public static RenderPlan Animating(TimeSpan interval) => new() { ShouldRedraw = true, Interval = interval };

    public bool Equals(RenderPlan other) => ShouldRedraw == other.ShouldRedraw && Interval == other.Interval;
    public override bool Equals(object? obj) => obj is RenderPlan p && Equals(p);
    public override int GetHashCode() => HashCode.Combine(ShouldRedraw, Interval);
}

/// <summary>
/// 雙層計時器的渲染層決策（設計檔 §7.1 / §7.1.1）：<b>依當前播放單元的格數動態調整重繪頻率</b>，
/// 而非固定值。這解決 §4.3 的隱憂——<c>AllowsTransparency=True</c> 走軟體算圖，全視窗高頻重繪是
/// 效能瓶頸；讓全靜態素材時效能特性等同純靜態架構，只有真正在播動畫的那幾秒才吃 CPU。
/// </summary>
/// <remarks>
/// <b>§7.1.1 對照表：</b>
/// <list type="bullet">
///   <item><description><c>frames == 1</c>（靜態圖）→ <b>暫停重繪</b>，僅在單元切換時繪一次。</description></item>
///   <item><description><c>frames &gt; 1</c> 且 <c>loop == true</c> → 該單元的 fps（建議 12–15）。</description></item>
///   <item><description><c>frames &gt; 1</c> 且 <c>loop == false</c> → 該單元的 fps，<b>播到最後一格後暫停重繪</b>。</description></item>
/// </list>
/// 渲染頻率界限 1–15 Hz（<b>不用 30 fps</b>）。缺 <c>fps</c> 的多格單元視為無法播放而暫停，
/// 與 <see cref="SpriteSheetFrameMath"/>（fps ≤ 0 凍結第 0 格）保持一致。
/// <para>
/// <b>職責分工：</b>本類別只做「該不該重繪、間隔多少」的<b>純決策</b>；實際的 <c>DispatcherTimer</c>
/// 由渲染綁定層（D4）依 <see cref="RenderPlan"/> 設定 <c>Interval</c> 或停用，並在轉為暫停時補繪一次
/// 最後一格。狀態 tick 固定 1 Hz（見 <see cref="StateTickInterval"/>），與動態的渲染 tick 分離。
/// 本類別不依賴 WPF，可跨平台單元測試。
/// </para>
/// </remarks>
public sealed class RenderTickController
{
    /// <summary>狀態 tick 固定頻率（§7.1：1 Hz）。更新數值、判定心情、選單元、檢查輸入、自動保存。</summary>
    public static readonly TimeSpan StateTickInterval = TimeSpan.FromSeconds(1);

    /// <summary>渲染 tick 下限（§7.1.1：1 Hz）。</summary>
    public const int MinFps = 1;

    /// <summary>渲染 tick 上限（§7.1.1：15 Hz；不用 30 fps 因軟體算圖是瓶頸）。</summary>
    public const int MaxFps = 15;

    private readonly Func<DateTime> _now;
    private VisualUnitInfo? _unit;
    private DateTime _unitStart;

    /// <param name="clock">時鐘（預設 <see cref="DateTime.UtcNow"/>；用差值判斷非循環是否播完，避免 DST/校時跳動）。</param>
    public RenderTickController(Func<DateTime>? clock = null)
    {
        _now = clock ?? (() => DateTime.UtcNow);
        _unitStart = _now();
    }

    /// <summary>
    /// 單元切換時呼叫：記錄新單元與其起始時刻，回傳此單元的初始渲染計畫。
    /// D4 依回傳值設定渲染計時器（<see cref="RenderPlan.Paused"/> → 停用並繪一次；否則設 <c>Interval</c>）。
    /// </summary>
    /// <param name="unit">新的當前單元。</param>
    /// <param name="unitStart">單元起始時刻（通常取 <c>PetVisualSelector.ElapsedInUnit</c> 對應的起點；預設現在）。</param>
    public RenderPlan OnUnitChanged(VisualUnitInfo unit, DateTime? unitStart = null)
    {
        _unit = unit ?? throw new ArgumentNullException(nameof(unit));
        _unitStart = unitStart ?? _now();
        return Plan(unit, TimeSpan.Zero);
    }

    /// <summary>
    /// 於每個渲染 tick 前呼叫：依當前單元與經過時間重新評估渲染計畫
    /// （非循環動畫播完後會轉為 <see cref="RenderPlan.Paused"/>，讓 D4 停止重繪）。
    /// </summary>
    public RenderPlan Evaluate() =>
        _unit is null ? RenderPlan.Paused : Plan(_unit, _now() - _unitStart);

    /// <summary>
    /// 純決策：依單元格數／fps／loop 與經過時間，決定當前該不該週期重繪、間隔多少（§7.1.1）。
    /// </summary>
    public static RenderPlan Plan(VisualUnitInfo unit, TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(unit);

        // 靜態圖，或多格但缺有效 fps（無法播放）→ 暫停重繪。
        if (unit.Frames <= 1 || unit.Fps is not { } rawFps || rawFps <= 0)
            return RenderPlan.Paused;

        int fps = Math.Clamp(rawFps, MinFps, MaxFps);
        var interval = TimeSpan.FromSeconds(1.0 / fps);

        if (unit.Loops)
            return RenderPlan.Animating(interval); // 循環：持續重繪

        // 非循環：播到最後一格（自然長度 = frames / 原始 fps）後暫停。
        double naturalSec = (double)unit.Frames / rawFps;
        return elapsed.TotalSeconds >= naturalSec
            ? RenderPlan.Paused
            : RenderPlan.Animating(interval);
    }
}
