using DesktopPet.Core.Visuals;
using DesktopPet.Models;
using Xunit;

namespace DesktopPet.Tests;

/// <summary>
/// 驗證設計檔 §7.2.1 心情判定的關鍵不變量：三分支、順序不可換（飢餓優先於低能量）、
/// 完全不看 <c>Happiness</c>、門檻為嚴格 <c>&gt; 70</c> / <c>&lt; 20</c>（邊界值不觸發）。
/// 這些不變量寫錯會讓 <c>SAD</c>/<c>LOW_ENERGY</c> 分支在錯誤時機觸發或永不觸發。
/// </summary>
public class MoodEvaluatorTests
{
    private readonly MoodEvaluator _evaluator = new();

    private static Pet PetWith(int hunger, int energy, int happiness = 50) =>
        new() { Hunger = hunger, Energy = energy, Happiness = happiness };

    // ── 三分支基本判定 ─────────────────────────────────────────────
    [Theory]
    [InlineData(71, 50, PetMood.Sad)]         // Hunger > 70 → SAD
    [InlineData(100, 100, PetMood.Sad)]       // 極端飢餓仍是 SAD（能量充足不影響）
    [InlineData(50, 19, PetMood.LowEnergy)]   // Energy < 20 → LOW_ENERGY
    [InlineData(0, 0, PetMood.LowEnergy)]     // 不餓但沒能量 → LOW_ENERGY
    [InlineData(50, 50, PetMood.Neutral)]     // 皆非 → NEUTRAL
    [InlineData(0, 100, PetMood.Neutral)]     // 吃飽又有精神 → NEUTRAL
    public void EvaluateMood_follows_three_branch_rule(int hunger, int energy, PetMood expected)
    {
        Assert.Equal(expected, _evaluator.EvaluateMood(PetWith(hunger, energy)));
    }

    // ── 順序不可換：飢餓優先於低能量 ────────────────────────────────
    [Fact]
    public void Hunger_takes_precedence_over_low_energy_when_both_hold()
    {
        // Hunger > 70 且 Energy < 20 同時成立 → 必須顯示 SAD（§7.2.1）
        Assert.Equal(PetMood.Sad, _evaluator.EvaluateMood(PetWith(hunger: 90, energy: 5)));
    }

    // ── 門檻為「嚴格」比較：邊界值不觸發 ────────────────────────────
    [Fact]
    public void Hunger_exactly_at_threshold_is_not_sad()
    {
        // Hunger == 70 不算 SAD（條件是 > 70），能量正常 → NEUTRAL
        Assert.Equal(PetMood.Neutral, _evaluator.EvaluateMood(PetWith(hunger: 70, energy: 50)));
    }

    [Fact]
    public void Energy_exactly_at_threshold_is_not_low_energy()
    {
        // Energy == 20 不算 LOW_ENERGY（條件是 < 20）→ NEUTRAL
        Assert.Equal(PetMood.Neutral, _evaluator.EvaluateMood(PetWith(hunger: 50, energy: 20)));
    }

    // ── Happiness 完全不影響心情（外觀與幸福度解耦）────────────────
    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Happiness_does_not_affect_mood(int happiness)
    {
        // 相同 Hunger/Energy 下，無論 Happiness 為何都判為 NEUTRAL
        Assert.Equal(PetMood.Neutral, _evaluator.EvaluateMood(PetWith(hunger: 50, energy: 50, happiness: happiness)));
    }

    // ── 心情 → 視覺狀態映射 ────────────────────────────────────────
    [Theory]
    [InlineData(PetMood.Neutral, PetVisualState.Neutral)]
    [InlineData(PetMood.Sad, PetVisualState.Sad)]
    [InlineData(PetMood.LowEnergy, PetVisualState.LowEnergy)]
    public void ToVisualState_maps_each_mood(PetMood mood, PetVisualState expected)
    {
        Assert.Equal(expected, MoodEvaluator.ToVisualState(mood));
    }

    [Fact]
    public void EvaluateVisualState_composes_evaluate_and_map()
    {
        Assert.Equal(PetVisualState.Sad, _evaluator.EvaluateVisualState(PetWith(hunger: 80, energy: 50)));
        Assert.Equal(PetVisualState.LowEnergy, _evaluator.EvaluateVisualState(PetWith(hunger: 10, energy: 5)));
        Assert.Equal(PetVisualState.Neutral, _evaluator.EvaluateVisualState(PetWith(hunger: 10, energy: 90)));
    }

    [Fact]
    public void EvaluateMood_null_pet_throws()
    {
        Assert.Throws<ArgumentNullException>(() => _evaluator.EvaluateMood(null!));
    }
}
