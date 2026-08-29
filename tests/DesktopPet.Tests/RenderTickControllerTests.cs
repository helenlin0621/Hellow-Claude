using System;
using DesktopPet.Core.Skins;
using DesktopPet.Core.Visuals;
using Xunit;

namespace DesktopPet.Tests;

/// <summary>
/// 驗證設計檔 §7.1.1 動態渲染頻率：靜態圖暫停重繪、循環動畫依 fps 持續、非循環播完暫停；
/// fps 夾在 1–15 Hz（不用 30）；缺 fps 的多格單元視為無法播放而暫停（與推格數學一致）。
/// </summary>
public class RenderTickControllerTests
{
    private static VisualUnitInfo Static() => new() { Frames = 1 };
    private static VisualUnitInfo Loop(int frames, int fps) => new() { Frames = frames, Fps = fps, Loop = true, FrameWidth = 256 };
    private static VisualUnitInfo Once(int frames, int fps) => new() { Frames = frames, Fps = fps, Loop = false, FrameWidth = 256 };

    // ── 純決策 Plan ──────────────────────────────────────────────
    [Fact]
    public void Static_unit_is_paused()
    {
        Assert.False(RenderTickController.Plan(Static(), TimeSpan.Zero).ShouldRedraw);
    }

    [Fact]
    public void Looping_unit_animates_at_fps_regardless_of_elapsed()
    {
        var plan = RenderTickController.Plan(Loop(6, 12), TimeSpan.FromHours(1));
        Assert.True(plan.ShouldRedraw);
        Assert.Equal(TimeSpan.FromSeconds(1.0 / 12), plan.Interval);
    }

    [Fact]
    public void Non_looping_unit_animates_until_natural_end_then_pauses()
    {
        var unit = Once(8, 15); // 自然長度 = 8/15 ≈ 0.533s
        Assert.True(RenderTickController.Plan(unit, TimeSpan.FromSeconds(0.4)).ShouldRedraw);
        Assert.Equal(TimeSpan.FromSeconds(1.0 / 15), RenderTickController.Plan(unit, TimeSpan.Zero).Interval);
        Assert.False(RenderTickController.Plan(unit, TimeSpan.FromSeconds(0.6)).ShouldRedraw); // 播完 → 暫停
    }

    [Fact]
    public void Fps_is_clamped_to_max_15()
    {
        var plan = RenderTickController.Plan(Loop(4, 30), TimeSpan.Zero);
        Assert.Equal(TimeSpan.FromSeconds(1.0 / 15), plan.Interval); // 30 → 15
    }

    [Fact]
    public void Multiframe_without_fps_is_paused()
    {
        var unit = new VisualUnitInfo { Frames = 6, Loop = true }; // 缺 fps（異常）
        Assert.False(RenderTickController.Plan(unit, TimeSpan.Zero).ShouldRedraw);
    }

    // ── 具時鐘的 OnUnitChanged / Evaluate ────────────────────────
    [Fact]
    public void Evaluate_pauses_after_non_looping_animation_finishes()
    {
        var clock = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var controller = new RenderTickController(() => clock);

        var start = controller.OnUnitChanged(Once(8, 15), clock);
        Assert.True(start.ShouldRedraw);

        clock = clock.AddSeconds(0.4);
        Assert.True(controller.Evaluate().ShouldRedraw);

        clock = clock.AddSeconds(0.3); // 累計 0.7s > 0.533s
        Assert.False(controller.Evaluate().ShouldRedraw);
    }

    [Fact]
    public void Evaluate_before_any_unit_is_paused()
    {
        Assert.False(new RenderTickController().Evaluate().ShouldRedraw);
    }

    // ── 常數 ─────────────────────────────────────────────────────
    [Fact]
    public void State_tick_is_one_hertz()
    {
        Assert.Equal(TimeSpan.FromSeconds(1), RenderTickController.StateTickInterval);
    }
}
