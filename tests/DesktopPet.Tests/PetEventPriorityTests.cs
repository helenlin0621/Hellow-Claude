using System;
using DesktopPet.Core.Visuals;
using Xunit;

namespace DesktopPet.Tests;

/// <summary>
/// 驗證設計檔 §7.3.2 事件 vs 心情的優先權狀態機：事件 &gt; 心情、進行中不被打斷、
/// 「至少 N 秒」滿足後自動回到心情、持續型（<c>requiredDurationSec &lt;= 0</c>，睡眠）不自動結束、
/// <c>EndEvent</c> 可強制結束。
/// </summary>
public class PetEventPriorityTests
{
    private DateTime _clock = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private DateTime Now() => _clock;
    private void Advance(double seconds) => _clock = _clock.AddSeconds(seconds);

    private PetEventPriority NewSut() => new(Now);

    [Fact]
    public void Resolve_returns_mood_when_no_active_event()
    {
        var sut = NewSut();
        Assert.Equal(PetVisualState.Sad, sut.Resolve(PetVisualState.Sad));
        Assert.False(sut.HasActiveEvent);
    }

    [Fact]
    public void TryTrigger_succeeds_when_idle()
    {
        var sut = NewSut();
        Assert.True(sut.TryTrigger(PetVisualState.Feed, 2.5));
        Assert.True(sut.HasActiveEvent);
        Assert.Equal(PetVisualState.Feed, sut.Resolve(PetVisualState.Neutral)); // 事件優先於心情
    }

    [Fact]
    public void TryTrigger_is_ignored_while_an_event_is_in_progress()
    {
        var sut = NewSut();
        Assert.True(sut.TryTrigger(PetVisualState.Feed, 2.5));

        Advance(0.5);
        Assert.False(sut.TryTrigger(PetVisualState.Click, 1.5)); // 進行中不被打斷（§7.3.2）

        Assert.Equal(PetVisualState.Feed, sut.Resolve(PetVisualState.Neutral)); // 仍是 FEED，未被 CLICK 蓋掉
    }

    [Fact]
    public void Event_stays_active_before_required_duration_elapses()
    {
        var sut = NewSut();
        sut.TryTrigger(PetVisualState.Click, 1.5);

        Advance(1.0); // 明顯未達 1.5s 門檻
        Assert.Equal(PetVisualState.Click, sut.Resolve(PetVisualState.Sad));
        Assert.True(sut.HasActiveEvent);
    }

    [Fact]
    public void Event_reverts_to_mood_once_required_duration_elapses()
    {
        var sut = NewSut();
        sut.TryTrigger(PetVisualState.Click, 1.5);

        Advance(2.0); // 明顯超過 1.5s 門檻
        Assert.Equal(PetVisualState.Sad, sut.Resolve(PetVisualState.Sad)); // 已結束，回到心情
        Assert.False(sut.HasActiveEvent);
    }

    [Fact]
    public void Boundary_at_exactly_required_duration_counts_as_finished()
    {
        var sut = NewSut();
        sut.TryTrigger(PetVisualState.Feed, 2.5);
        Advance(2.5); // 恰好等於門檻 → 已結束（>=）
        Assert.Equal(PetVisualState.Neutral, sut.Resolve(PetVisualState.Neutral));
    }

    [Fact]
    public void Persistent_event_does_not_auto_end_even_after_a_long_time()
    {
        var sut = NewSut();
        sut.TryTrigger(PetVisualState.Sleep, 0); // durationSec<=0：持續型（§7.3.3：睡眠直到條件解除）

        Advance(24 * 3600); // 一整天過去
        Assert.Equal(PetVisualState.Sleep, sut.Resolve(PetVisualState.LowEnergy));
        Assert.True(sut.HasActiveEvent);
    }

    [Fact]
    public void EndEvent_forcibly_ends_a_persistent_event()
    {
        var sut = NewSut();
        sut.TryTrigger(PetVisualState.Sleep, 0);

        sut.EndEvent(); // 例如 Energy 回滿，由上層（E1/E4）呼叫
        Assert.False(sut.HasActiveEvent);
        Assert.Equal(PetVisualState.Neutral, sut.Resolve(PetVisualState.Neutral));
    }

    [Fact]
    public void EndEvent_on_idle_state_is_a_no_op()
    {
        var sut = NewSut();
        sut.EndEvent(); // 無進行中事件
        Assert.False(sut.HasActiveEvent);
    }

    [Fact]
    public void New_trigger_succeeds_after_previous_event_naturally_ends()
    {
        var sut = NewSut();
        sut.TryTrigger(PetVisualState.Click, 1.5);
        Advance(1.5);
        sut.Refresh(); // 或由 Resolve 觸發；此處直接呼叫驗證 Refresh 本身的行為

        Assert.True(sut.TryTrigger(PetVisualState.Feed, 2.5));
        Assert.Equal(PetVisualState.Feed, sut.Resolve(PetVisualState.Neutral));
    }
}
