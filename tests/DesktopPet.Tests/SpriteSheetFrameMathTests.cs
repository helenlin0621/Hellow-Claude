using System;
using DesktopPet.Core.Skins;
using Xunit;

namespace DesktopPet.Tests;

/// <summary>
/// 驗證設計檔 §7.3.5「格數決定」的推格數學：<c>idx = floor(秒 × fps)</c>，
/// <c>loop</c> 循環取模、非 <c>loop</c> 播完停最後一格；靜態圖／缺 fps／時間 0 皆回第 0 格。
/// 這是 Sprite Sheet 最易出錯的一段（取模／夾界），故獨立為純函式重點測試。
/// </summary>
/// <remarks>
/// 取樣刻意落在「格中點」（每格 0.1s，取 x.x5s），避開精確格邊界的浮點抖動
/// （例如 <c>0.7 × 10</c> 在 IEEE754 下為 6.999…→6）——該抖動對 12–15fps 桌寵動畫無感，
/// 但會讓邊界測試不穩定，故不在邊界取樣。
/// </remarks>
public class SpriteSheetFrameMathTests
{
    private static TimeSpan Sec(double s) => TimeSpan.FromSeconds(s);

    // ── 基本推格（fps=10, 8 格；每格 0.1s，取格中點）─────────────
    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(0.05, 0)]   // 第 0 格中點
    [InlineData(0.15, 1)]   // 第 1 格中點
    [InlineData(0.35, 3)]
    [InlineData(0.75, 7)]   // 最後一格中點
    public void FrameIndex_advances_with_elapsed(double seconds, int expected)
    {
        Assert.Equal(expected, SpriteSheetFrameMath.FrameIndex(Sec(seconds), fps: 10, frames: 8, loop: true));
    }

    // ── loop=true：超過總長度後循環取模 ────────────────────────
    [Theory]
    [InlineData(0.85, 0)]   // idx=8 → 8 % 8 = 0
    [InlineData(0.95, 1)]   // idx=9 → 1
    [InlineData(1.75, 1)]   // idx=17 → 17 % 8 = 1
    public void FrameIndex_loops_with_modulo(double seconds, int expected)
    {
        Assert.Equal(expected, SpriteSheetFrameMath.FrameIndex(Sec(seconds), fps: 10, frames: 8, loop: true));
    }

    // ── loop=false：播完停在最後一格 ────────────────────────────
    [Theory]
    [InlineData(0.75, 7)]   // 最後一格中點
    [InlineData(0.85, 7)]   // idx=8 → min(8, 7) = 7
    [InlineData(5.00, 7)]   // 很久之後仍停在第 7 格
    public void FrameIndex_clamps_to_last_frame_when_not_looping(double seconds, int expected)
    {
        Assert.Equal(expected, SpriteSheetFrameMath.FrameIndex(Sec(seconds), fps: 10, frames: 8, loop: false));
    }

    // ── 退化情況一律回第 0 格、不丟例外 ─────────────────────────
    [Theory]
    [InlineData(1, 0, 15, true)]    // frames <= 1（靜態圖）
    [InlineData(8, 0, 5.0, true)]   // fps <= 0（異常缺 fps）
    [InlineData(8, 15, -1.0, true)] // 負的 elapsed
    public void FrameIndex_degenerate_cases_return_zero(int frames, int fps, double seconds, bool loop)
    {
        Assert.Equal(0, SpriteSheetFrameMath.FrameIndex(Sec(seconds), fps, frames, loop));
    }

    // ── 長時間循環不溢位（loop 下持續累加）──────────────────────
    [Fact]
    public void FrameIndex_does_not_overflow_for_long_elapsed()
    {
        // 一天 @ 15fps ≈ 1.3M 格；以 long 承接後取模，結果需落在 [0, frames)
        int idx = SpriteSheetFrameMath.FrameIndex(TimeSpan.FromDays(1), fps: 15, frames: 8, loop: true);
        Assert.InRange(idx, 0, 7);
    }
}
