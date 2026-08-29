using DesktopPet.Utils;
using Xunit;

namespace DesktopPet.Tests;

/// <summary>
/// 驗證設計檔 §6.1 / §10.2 視窗落點純幾何：夾進工作區（不遮工作列）、多監視器（非零起點的工作區）、
/// 極小螢幕的左上對齊、預設右下角落點。單位無關（實體像素或 DIU 皆同一套運算）。
/// </summary>
public class WindowPositioningTests
{
    // 典型 1920x1080、底部 40px 工作列 → 工作區高 1040，起點 (0,0)。
    private static readonly RectD PrimaryWork = new(0, 0, 1920, 1040);

    // ── ClampToWorkArea ─────────────────────────────────────────

    [Fact]
    public void Window_already_inside_is_unchanged()
    {
        var window = new RectD(100, 100, 320, 240);
        Assert.Equal(window, WindowPositioning.ClampToWorkArea(window, PrimaryWork));
    }

    [Fact]
    public void Overflowing_bottom_is_pushed_up_to_clear_taskbar()
    {
        // 下緣 1020+240=1260 超出工作區底 1040 → 上推到 Top=800（1040-240），不壓到工作列。
        var window = new RectD(100, 1020, 320, 240);
        var clamped = WindowPositioning.ClampToWorkArea(window, PrimaryWork);
        Assert.Equal(800, clamped.Top);
        Assert.Equal(PrimaryWork.Bottom, clamped.Bottom); // 下緣貼齊工作區底
        Assert.Equal(100, clamped.Left);                  // 水平不動
    }

    [Fact]
    public void Overflowing_right_is_pushed_left()
    {
        var window = new RectD(1800, 100, 320, 240);
        var clamped = WindowPositioning.ClampToWorkArea(window, PrimaryWork);
        Assert.Equal(1600, clamped.Left); // 1920-320
        Assert.Equal(PrimaryWork.Right, clamped.Right);
    }

    [Fact]
    public void Negative_offscreen_is_pulled_back_to_origin()
    {
        var window = new RectD(-50, -30, 320, 240);
        var clamped = WindowPositioning.ClampToWorkArea(window, PrimaryWork);
        Assert.Equal(0, clamped.Left);
        Assert.Equal(0, clamped.Top);
    }

    [Fact]
    public void Secondary_monitor_with_nonzero_origin_is_respected()
    {
        // 右接的第二螢幕：工作區起點 (1920,0)，寬 1920。放在其外側 → 夾回該螢幕內。
        var secondary = new RectD(1920, 0, 1920, 1040);
        var window = new RectD(3900, 500, 320, 240); // 右緣超出 3840
        var clamped = WindowPositioning.ClampToWorkArea(window, secondary);
        Assert.Equal(secondary.Left + secondary.Width - 320, clamped.Left); // 3520
        Assert.True(clamped.Left >= secondary.Left);
    }

    [Fact]
    public void Window_larger_than_workarea_aligns_to_top_left()
    {
        var tiny = new RectD(0, 0, 200, 150);
        var window = new RectD(50, 50, 320, 240); // 比工作區大
        var clamped = WindowPositioning.ClampToWorkArea(window, tiny);
        Assert.Equal(tiny.Left, clamped.Left);
        Assert.Equal(tiny.Top, clamped.Top);
    }

    // ── DefaultPlacement ────────────────────────────────────────

    [Fact]
    public void Default_placement_is_bottom_right_with_margin()
    {
        var placed = WindowPositioning.DefaultPlacement(PrimaryWork, 320, 240, margin: 24);
        Assert.Equal(1920 - 320 - 24, placed.Left); // 1576
        Assert.Equal(1040 - 240 - 24, placed.Top);  // 776
    }

    [Fact]
    public void Default_placement_stays_within_workarea()
    {
        var placed = WindowPositioning.DefaultPlacement(PrimaryWork, 320, 240);
        Assert.True(placed.Left >= PrimaryWork.Left);
        Assert.True(placed.Top >= PrimaryWork.Top);
        Assert.True(placed.Right <= PrimaryWork.Right);
        Assert.True(placed.Bottom <= PrimaryWork.Bottom);
    }

    [Fact]
    public void Default_placement_on_secondary_monitor_lands_on_that_monitor()
    {
        var secondary = new RectD(1920, 0, 1920, 1040);
        var placed = WindowPositioning.DefaultPlacement(secondary, 320, 240, margin: 24);
        Assert.Equal(3840 - 320 - 24, placed.Left); // 3496，落在第二螢幕右下
        Assert.True(placed.Left >= secondary.Left);
        Assert.True(placed.Right <= secondary.Right);
    }
}
