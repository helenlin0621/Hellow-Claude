using DesktopPet.Core.Interaction;
using Xunit;

namespace DesktopPet.Tests;

/// <summary>
/// 驗證設計檔 §6.5.4 的距離與觸發條件：greet（接近 + 雙方閒置）、cuddle（持續接近超過門檻）、
/// 「接近」共用同一個 100px 門檻（設計檔只在 greet 給出具體數字）。皆為純邏輯，跨平台可跑。
/// </summary>
public class InteractionRulesTests
{
    // ── 距離與「接近」門檻 ────────────────────────────────────────
    [Fact]
    public void Distance_computes_euclidean_distance()
    {
        Assert.Equal(5, InteractionRules.Distance(0, 0, 3, 4));
        Assert.Equal(0, InteractionRules.Distance(10, 10, 10, 10));
    }

    [Theory]
    [InlineData(99, true)]
    [InlineData(100, false)]  // 嚴格小於
    [InlineData(101, false)]
    public void IsClose_uses_strict_less_than_100px(double distance, bool expectedClose)
    {
        Assert.Equal(expectedClose, InteractionRules.IsClose(distance));
    }

    // ── greet：接近 + 雙方閒置 ────────────────────────────────────
    [Theory]
    [InlineData(50, true, true, true)]     // 接近且雙方閒置 → 觸發
    [InlineData(150, true, true, false)]   // 太遠 → 不觸發，即使雙方閒置
    [InlineData(50, false, true, false)]   // A 忙碌 → 不觸發
    [InlineData(50, true, false, false)]   // B 忙碌 → 不觸發
    [InlineData(50, false, false, false)]  // 雙方都忙碌 → 不觸發
    public void ShouldGreet_requires_close_and_both_idle(double distance, bool aIdle, bool bIdle, bool expected)
    {
        Assert.Equal(expected, InteractionRules.ShouldGreet(distance, aIdle, bIdle));
    }

    // ── cuddle：持續接近的累計秒數 ──────────────────────────────────
    [Fact]
    public void TickCloseSeconds_accumulates_while_close_and_resets_otherwise()
    {
        int seconds = 0;
        for (int i = 0; i < 5; i++)
            seconds = InteractionRules.TickCloseSeconds(seconds, isClose: true);
        Assert.Equal(5, seconds);

        seconds = InteractionRules.TickCloseSeconds(seconds, isClose: false); // 離開接近距離 → 歸零
        Assert.Equal(0, seconds);
    }

    [Theory]
    [InlineData(599, false)]
    [InlineData(600, false)]  // 嚴格大於，剛好 10 分鐘不算
    [InlineData(601, true)]
    public void ShouldCuddle_requires_strictly_more_than_ten_minutes(int closeSeconds, bool expected)
    {
        Assert.Equal(expected, InteractionRules.ShouldCuddle(closeSeconds));
    }
}
