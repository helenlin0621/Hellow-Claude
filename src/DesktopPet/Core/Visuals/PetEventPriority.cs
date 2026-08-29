namespace DesktopPet.Core.Visuals;

/// <summary>
/// 事件 vs 心情的優先權與「進行中不被打斷」狀態機（設計檔 §7.3.2）。
/// </summary>
/// <remarks>
/// <b>只做「該不該切換、切完何時結束」的純決策</b>，不知道單元／畫面／計時器——那些是
/// <c>Core/AnimationManager.cs</c>（D4）的職責，本類別只回答「現在該顯示事件還是心情」。
/// <list type="bullet">
///   <item><description><b>優先權</b>：有進行中事件時一律回傳該事件，蓋過心情（§7.3.2「事件圖片 &gt; 心情圖片」）。</description></item>
///   <item><description><b>進行中不被打斷</b>：<see cref="TryTrigger"/> 在已有事件進行中時直接忽略新觸發
///     （回傳 <c>false</c>），例如 FEED 播放期間的新 CLICK 不會蓋掉 FEED。</description></item>
///   <item><description><b>至少 N 秒</b>：呼叫端已算好 <c>max(durationSec, 動畫自然長度)</c> 傳入
///     <c>requiredDurationSec</c>（本類別不知道格數/fps，那是 <c>VisualUnitInfo</c> 的事）；滿足後
///     由 <see cref="Resolve"/>（內部呼叫 <see cref="Refresh"/>）自動結束、交回心情。</description></item>
///   <item><description><b>持續型事件</b>（<c>requiredDurationSec == 0</c>，目前僅 SLEEP）：不會自動結束，
///     需外部在條件解除時明確呼叫 <see cref="EndEvent"/>（例如 Energy 回滿——屬 E1/E4，本任務不判定）。</description></item>
/// </list>
/// 純邏輯，不依賴 WPF，可跨平台單元測試；非執行緒安全（單一寵物 UI 執行緒）。
/// </remarks>
public sealed class PetEventPriority
{
    private readonly Func<DateTime> _now;

    private PetVisualState? _activeEvent;
    private DateTime _eventStart;
    private double _requiredDurationSec;

    /// <param name="clock">時鐘（預設 <see cref="DateTime.UtcNow"/>）。可注入以利測試。</param>
    public PetEventPriority(Func<DateTime>? clock = null)
    {
        _now = clock ?? (() => DateTime.UtcNow);
    }

    /// <summary>目前是否有事件正在進行中（優先於心情）。</summary>
    public bool HasActiveEvent => _activeEvent is not null;

    /// <summary>
    /// 嘗試觸發一個事件。已有事件進行中時忽略（§7.3.2「進行中不被打斷」），回傳 <c>false</c>；
    /// 否則開始播放，回傳 <c>true</c>。
    /// </summary>
    /// <param name="requestedEvent">Click/Feed/Sleep 之一（由呼叫端保證，本類別不驗證）。</param>
    /// <param name="requiredDurationSec">
    /// 此次播放所需的最短秒數，語意為「至少 N 秒」（§7.3.2：<c>max(durationSec, frames/fps)</c>，
    /// 由呼叫端算好傳入）。<c>&lt;= 0</c> 代表持續型（睡眠，直到條件解除），不會自動結束。
    /// </param>
    public bool TryTrigger(PetVisualState requestedEvent, double requiredDurationSec)
    {
        if (HasActiveEvent)
            return false;

        _activeEvent = requestedEvent;
        _eventStart = _now();
        _requiredDurationSec = requiredDurationSec;
        return true;
    }

    /// <summary>
    /// 若進行中事件已滿足最短秒數，結束它、回到心情。持續型事件（<c>requiredDurationSec &lt;= 0</c>）
    /// 不受此影響，只能靠 <see cref="EndEvent"/> 結束。<see cref="Resolve"/> 內部已呼叫本方法，
    /// 一般不需自行呼叫。
    /// </summary>
    public void Refresh()
    {
        if (_activeEvent is null || _requiredDurationSec <= 0)
            return;

        if ((_now() - _eventStart).TotalSeconds >= _requiredDurationSec)
            _activeEvent = null;
    }

    /// <summary>
    /// 強制結束目前的持續型事件（例如 SLEEP 的「醒來」條件達成時，由上層呼叫）。
    /// 無進行中事件時為 no-op。
    /// </summary>
    public void EndEvent() => _activeEvent = null;

    /// <summary>
    /// 目前該顯示的狀態：先 <see cref="Refresh"/>，事件仍進行中則回傳該事件，否則回傳傳入的
    /// <paramref name="mood"/>（§7.3.2 優先權：事件 &gt; 心情）。
    /// </summary>
    /// <param name="mood">當前心情態（僅 Neutral/Sad/LowEnergy 有意義，由呼叫端保證）。</param>
    public PetVisualState Resolve(PetVisualState mood)
    {
        Refresh();
        return _activeEvent ?? mood;
    }
}
