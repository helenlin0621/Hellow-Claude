using System;
using DesktopPet.Core.Interaction;
using Xunit;

namespace DesktopPet.Tests;

/// <summary>
/// 驗證 E3 互動觸發規則（<see cref="InteractionRules"/>，設計檔 §6.5.4）：距離判定、
/// cuddle 需長時間維持接近、greet 需雙方閒置、play 為接近時候補、交集為空或超距一律不觸發。
/// </summary>
public class InteractionRulesTests
{
    private static readonly string[] All = { "greet", "play", "cuddle" };

    [Fact]
    public void Distance_is_euclidean()
    {
        Assert.Equal(5.0, InteractionRules.Distance(0, 0, 3, 4), 6);
    }

    [Theory]
    [InlineData(99.0, true)]
    [InlineData(100.0, false)] // 嚴格 < 100
    [InlineData(150.0, false)]
    public void IsClose_uses_strict_threshold(double distance, bool expected)
    {
        Assert.Equal(expected, InteractionRules.IsClose(distance));
    }

    [Fact]
    public void OutOfRange_never_triggers()
    {
        var type = InteractionRules.ChooseInteractionType(All, distance: 200, sustainedCloseSeconds: 99999, bothIdle: true);
        Assert.Null(type);
    }

    [Fact]
    public void EmptyIntersection_never_triggers()
    {
        var type = InteractionRules.ChooseInteractionType(Array.Empty<string>(), distance: 10, sustainedCloseSeconds: 99999, bothIdle: true);
        Assert.Null(type);
    }

    [Fact]
    public void Cuddle_when_sustained_close_long_enough()
    {
        var type = InteractionRules.ChooseInteractionType(
            All, distance: 10,
            sustainedCloseSeconds: InteractionRules.CuddleSustainedSeconds + 1,
            bothIdle: true);
        Assert.Equal("cuddle", type);
    }

    [Fact]
    public void Greet_when_close_idle_and_not_yet_sustained_for_cuddle()
    {
        var type = InteractionRules.ChooseInteractionType(
            All, distance: 10,
            sustainedCloseSeconds: 0,
            bothIdle: true);
        Assert.Equal("greet", type);
    }

    [Fact]
    public void Greet_requires_both_idle_else_falls_to_play()
    {
        // 非閒置 → 不 greet；但交集含 play → 退為 play（§6.5.4 接近時的候補）。
        var type = InteractionRules.ChooseInteractionType(
            All, distance: 10,
            sustainedCloseSeconds: 0,
            bothIdle: false);
        Assert.Equal("play", type);
    }

    [Fact]
    public void Play_when_only_play_available()
    {
        var type = InteractionRules.ChooseInteractionType(
            new[] { "play" }, distance: 10,
            sustainedCloseSeconds: 0,
            bothIdle: true);
        Assert.Equal("play", type);
    }

    [Fact]
    public void Cuddle_not_chosen_when_absent_from_intersection()
    {
        // 長時間接近但交集無 cuddle，且雙方閒置 → 退為 greet。
        var type = InteractionRules.ChooseInteractionType(
            new[] { "greet" }, distance: 10,
            sustainedCloseSeconds: InteractionRules.CuddleSustainedSeconds + 1,
            bothIdle: true);
        Assert.Equal("greet", type);
    }

    [Fact]
    public void No_matching_condition_returns_null()
    {
        // 只有 cuddle 素材，但尚未維持夠久 → 無可觸發（greet/play 皆不在交集）。
        var type = InteractionRules.ChooseInteractionType(
            new[] { "cuddle" }, distance: 10,
            sustainedCloseSeconds: 0,
            bothIdle: true);
        Assert.Null(type);
    }
}
