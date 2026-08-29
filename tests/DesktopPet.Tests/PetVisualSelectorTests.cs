using System;
using System.Collections.Generic;
using System.Linq;
using DesktopPet.Core.Visuals;
using Xunit;

namespace DesktopPet.Tests;

/// <summary>
/// 驗證設計檔 §7.3.5 單元選擇：只在「狀態改變或 reroll 到期」才重抽（否則維持同一單元）、
/// 多單元避免連續抽到同一個、切換時重設 <see cref="PetVisualSelector.ElapsedInUnit"/>；
/// 缺素材走 §7.3.4 fallback，null fallback（CLICK/FEED）不換圖且<b>不重設時間軸</b>。
/// 以可控時鐘與確定性抽籤委派測試，跨平台可跑。
/// </summary>
public class PetVisualSelectorTests
{
    private static readonly VisualRegistry Registry =
        VisualRegistry.FromDefinitions(VisualRegistry.DefaultDefinitions());
    private static readonly VisualFallbackResolver Fallback = new(Registry);

    private DateTime _clock = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private DateTime Now() => _clock;
    private void Advance(double seconds) => _clock = _clock.AddSeconds(seconds);

    private static IReadOnlyDictionary<PetVisualState, IReadOnlyList<string>> Pool(
        params (PetVisualState state, string[] units)[] entries) =>
        entries.ToDictionary(e => e.state, e => (IReadOnlyList<string>)e.units);

    /// <summary>抽籤永遠取第 0 個候選（讓避免重複的行為可預測）。</summary>
    private PetVisualSelector Selector(
        IReadOnlyDictionary<PetVisualState, IReadOnlyList<string>> pool, Func<int, int>? choose = null) =>
        new(pool, Fallback, Now, choose ?? (_ => 0));

    // ── 首抽 ─────────────────────────────────────────────────────
    [Fact]
    public void First_call_picks_from_requested_state_pool()
    {
        var sel = Selector(Pool((PetVisualState.Neutral, new[] { "anim_idle_1" })));

        Assert.Equal("anim_idle_1", sel.ResolveUnit(PetVisualState.Neutral, 8));
        Assert.Equal("anim_idle_1", sel.CurrentUnit);
        Assert.Equal(PetVisualState.Neutral, sel.CurrentState);
    }

    // ── 狀態不變且未到 reroll → 維持同一單元、時間軸持續累加 ─────
    [Fact]
    public void Keeps_same_unit_when_state_unchanged_and_within_interval()
    {
        var sel = Selector(Pool((PetVisualState.Neutral, new[] { "a", "b", "c" })));
        Assert.Equal("a", sel.ResolveUnit(PetVisualState.Neutral, 8));

        Advance(3); // < 8s
        Assert.Equal("a", sel.ResolveUnit(PetVisualState.Neutral, 8));
        Assert.Equal(TimeSpan.FromSeconds(3), sel.ElapsedInUnit); // 未重設，持續累加
    }

    // ── reroll 到期 → 重抽（避開當前）並重設時間軸 ────────────────
    [Fact]
    public void Rerolls_and_resets_timeline_when_interval_elapsed()
    {
        var sel = Selector(Pool((PetVisualState.Neutral, new[] { "a", "b", "c" })));
        Assert.Equal("a", sel.ResolveUnit(PetVisualState.Neutral, 8));

        Advance(8);
        var next = sel.ResolveUnit(PetVisualState.Neutral, 8);
        Assert.NotEqual("a", next);                       // 避免連續抽到同一個
        Assert.Equal(TimeSpan.Zero, sel.ElapsedInUnit);   // 時間軸重設
    }

    [Fact]
    public void Reroll_never_repeats_the_same_unit_consecutively()
    {
        var sel = Selector(Pool((PetVisualState.Neutral, new[] { "a", "b", "c" })));
        var seen = new List<string?> { sel.ResolveUnit(PetVisualState.Neutral, 8) };

        for (int i = 0; i < 5; i++)
        {
            Advance(8);
            seen.Add(sel.ResolveUnit(PetVisualState.Neutral, 8));
        }

        for (int i = 1; i < seen.Count; i++)
            Assert.NotEqual(seen[i - 1], seen[i]);
    }

    // ── 狀態改變 → 換到新狀態的單元、重設時間軸 ──────────────────
    [Fact]
    public void State_change_switches_pool_and_resets_timeline()
    {
        var sel = Selector(Pool(
            (PetVisualState.Neutral, new[] { "anim_idle_1" }),
            (PetVisualState.Sad, new[] { "anim_sad_1" })));

        sel.ResolveUnit(PetVisualState.Neutral, 8);
        Advance(2);
        Assert.Equal("anim_sad_1", sel.ResolveUnit(PetVisualState.Sad, 0));
        Assert.Equal(PetVisualState.Sad, sel.CurrentState);
        Assert.Equal(TimeSpan.Zero, sel.ElapsedInUnit);
    }

    // ── 缺素材 → fallback（§7.3.4）──────────────────────────────
    [Fact]
    public void Missing_state_falls_back_to_available_state_units()
    {
        // 只有 NEUTRAL 有素材，要求 SAD → 退回 NEUTRAL 的單元
        var sel = Selector(Pool((PetVisualState.Neutral, new[] { "anim_idle_1" })));

        Assert.Equal("anim_idle_1", sel.ResolveUnit(PetVisualState.Sad, 0));
        Assert.Equal(PetVisualState.Sad, sel.CurrentState); // 記錄被要求的狀態
    }

    // ── null fallback（CLICK/FEED 缺圖）→ 不換圖、不重設時間軸 ───
    [Fact]
    public void Null_fallback_keeps_current_unit_without_resetting_timeline()
    {
        var sel = Selector(Pool((PetVisualState.Neutral, new[] { "anim_idle_1" })));
        sel.ResolveUnit(PetVisualState.Neutral, 8);

        Advance(2);
        var result = sel.ResolveUnit(PetVisualState.Click, 0); // Click 缺圖、fallback=null

        Assert.Equal("anim_idle_1", result);                 // 維持目前畫面
        Assert.Equal(PetVisualState.Neutral, sel.CurrentState); // 未切換到 Click
        Assert.Equal(TimeSpan.FromSeconds(2), sel.ElapsedInUnit); // 時間軸未重設
    }

    // ── 單一單元 → 每次都回它（含 reroll），不丟例外 ─────────────
    [Fact]
    public void Single_unit_is_always_returned_even_on_reroll()
    {
        var sel = Selector(Pool((PetVisualState.Neutral, new[] { "only" })));
        Assert.Equal("only", sel.ResolveUnit(PetVisualState.Neutral, 8));

        Advance(8);
        Assert.Equal("only", sel.ResolveUnit(PetVisualState.Neutral, 8));
    }

    // ── 全缺（含 NEUTRAL）→ null，不崩潰 ────────────────────────
    [Fact]
    public void No_units_at_all_resolves_to_null()
    {
        var sel = Selector(Pool());
        Assert.Null(sel.ResolveUnit(PetVisualState.Sad, 0));
        Assert.Null(sel.CurrentUnit);
    }

    // ── 防呆 ─────────────────────────────────────────────────────
    [Fact]
    public void Constructor_rejects_null_arguments()
    {
        var pool = Pool((PetVisualState.Neutral, new[] { "a" }));
        Assert.Throws<ArgumentNullException>(() => new PetVisualSelector(null!, Fallback));
        Assert.Throws<ArgumentNullException>(() => new PetVisualSelector(pool, null!));
    }
}
