using System.Collections.Generic;
using DesktopPet.Core.Visuals;
using Xunit;

namespace DesktopPet.Tests;

/// <summary>
/// 驗證設計檔 §7.3.4 fallback 鏈：SAD/LOW_ENERGY→NEUTRAL、SLEEP→LOW_ENERGY→NEUTRAL、
/// CLICK/FEED→不換圖（null）、有素材時回自身、整鏈皆缺或成環時回 null（不崩潰）。
/// </summary>
public class VisualFallbackResolverTests
{
    private static readonly VisualRegistry Registry =
        VisualRegistry.FromDefinitions(VisualRegistry.DefaultDefinitions());

    private static readonly VisualFallbackResolver Resolver = new(Registry);

    /// <summary>以「有素材的狀態集合」建立 hasUnits 委派。</summary>
    private static Func<PetVisualState, bool> Available(params PetVisualState[] present)
    {
        var set = new HashSet<PetVisualState>(present);
        return set.Contains;
    }

    [Fact]
    public void State_with_units_resolves_to_itself()
    {
        var result = Resolver.Resolve(PetVisualState.Sad, Available(PetVisualState.Sad, PetVisualState.Neutral));
        Assert.Equal(PetVisualState.Sad, result);
    }

    [Theory]
    [InlineData(PetVisualState.Sad)]
    [InlineData(PetVisualState.LowEnergy)]
    public void Missing_mood_falls_back_to_neutral(PetVisualState missing)
    {
        // 只有 NEUTRAL 有素材 → 缺的心情退回 NEUTRAL
        var result = Resolver.Resolve(missing, Available(PetVisualState.Neutral));
        Assert.Equal(PetVisualState.Neutral, result);
    }

    [Fact]
    public void Sleep_falls_back_to_low_energy_when_present()
    {
        var result = Resolver.Resolve(PetVisualState.Sleep, Available(PetVisualState.LowEnergy, PetVisualState.Neutral));
        Assert.Equal(PetVisualState.LowEnergy, result);
    }

    [Fact]
    public void Sleep_falls_back_through_low_energy_to_neutral()
    {
        // SLEEP 缺、LOW_ENERGY 也缺 → 再退到 NEUTRAL
        var result = Resolver.Resolve(PetVisualState.Sleep, Available(PetVisualState.Neutral));
        Assert.Equal(PetVisualState.Neutral, result);
    }

    [Theory]
    [InlineData(PetVisualState.Click)]
    [InlineData(PetVisualState.Feed)]
    public void Missing_event_with_null_fallback_returns_null(PetVisualState evt)
    {
        // CLICK/FEED 缺圖 → 不換圖（維持目前畫面）
        var result = Resolver.Resolve(evt, Available(PetVisualState.Neutral));
        Assert.Null(result);
    }

    [Fact]
    public void All_missing_including_neutral_returns_null_without_throwing()
    {
        // 退化情況：連 NEUTRAL 都缺（匯入階段本應擋下）→ 回 null，不崩潰
        var result = Resolver.Resolve(PetVisualState.Sad, Available(/* 全缺 */));
        Assert.Null(result);
    }

    [Fact]
    public void Cyclic_fallback_configuration_terminates_with_null()
    {
        // 人為設成環：A→B→A，全部無素材 → 應中止並回 null
        var cyclic = VisualRegistry.FromDefinitions(new[]
        {
            new VisualTypeDefinition { State = PetVisualState.Sad,       Prefix = "a", Fallback = PetVisualState.LowEnergy },
            new VisualTypeDefinition { State = PetVisualState.LowEnergy, Prefix = "b", Fallback = PetVisualState.Sad },
        });
        var resolver = new VisualFallbackResolver(cyclic);

        Assert.Null(resolver.Resolve(PetVisualState.Sad, Available(/* 全缺 */)));
    }
}
