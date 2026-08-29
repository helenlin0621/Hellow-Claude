using System;
using DesktopPet.Core;
using DesktopPet.Models;
using Xunit;

namespace DesktopPet.Tests;

/// <summary>
/// 驗證設計檔 §7.4.2 / §7.4.3 幸福度的關鍵不變量：
/// 每小時疊加衰減（基礎 -1、飢餓 / 疲勞各 -1、冷落 -2，最壞 -5）、冷落以 <c>AwakeIdleSeconds</c> 判定、
/// 回補帶冷卻（餵食 +10/30 分、點擊 +2/60 秒、睡眠 +5/無、互動 +3/30 分）、冷卻只 gate 幸福度不 gate 操作、
/// 幸福度夾 0–100，且<b>不碰 Hunger/Energy/Health</b>。
/// </summary>
public class HappinessManagerTests
{
    private static readonly DateTime T0 = new(2026, 8, 29, 12, 0, 0, DateTimeKind.Local);

    private static Pet PetWith(int happiness = 50, int hunger = 50, int energy = 50, int awakeIdle = 0) =>
        new() { Happiness = happiness, Hunger = hunger, Energy = energy, AwakeIdleSeconds = awakeIdle };

    private static void Tick(HappinessManager hm, Pet pet, int times)
    {
        for (int i = 0; i < times; i++) hm.Tick(pet);
    }

    // ── §7.4.2 每小時衰減速率疊加 ─────────────────────────────────
    [Theory]
    [InlineData(50, 50, 0, 1)]                 // 一般：僅基礎 -1
    [InlineData(71, 50, 0, 2)]                 // + 飢餓（Hunger>70）
    [InlineData(50, 19, 0, 2)]                 // + 疲勞（Energy<20）
    [InlineData(50, 50, 4 * 3600 + 1, 3)]      // + 冷落（>4h）
    [InlineData(71, 19, 4 * 3600 + 1, 5)]      // 全部疊加 → 最壞 -5
    public void ComputeHourlyDecay_stacks_penalties(int hunger, int energy, int idle, int expectedRate)
    {
        var pet = PetWith(hunger: hunger, energy: energy, awakeIdle: idle);
        Assert.Equal(expectedRate, HappinessManager.ComputeHourlyDecay(pet));
    }

    [Theory]
    [InlineData(70, false)]  // Hunger==70 不觸發（嚴格 >）
    [InlineData(71, true)]
    public void Hunger_penalty_uses_strict_greater_than(int hunger, bool penalized)
    {
        var pet = PetWith(hunger: hunger, energy: 50, awakeIdle: 0);
        Assert.Equal(penalized ? 2 : 1, HappinessManager.ComputeHourlyDecay(pet));
    }

    [Theory]
    [InlineData(20, false)]  // Energy==20 不觸發（嚴格 <）
    [InlineData(19, true)]
    public void Energy_penalty_uses_strict_less_than(int energy, bool penalized)
    {
        var pet = PetWith(hunger: 50, energy: energy, awakeIdle: 0);
        Assert.Equal(penalized ? 2 : 1, HappinessManager.ComputeHourlyDecay(pet));
    }

    [Theory]
    [InlineData(4 * 3600, false)] // 恰 4h 不觸發（嚴格「超過」）
    [InlineData(4 * 3600 + 1, true)]
    public void Idle_penalty_uses_strict_greater_than_four_hours(int idle, bool penalized)
    {
        var pet = PetWith(hunger: 50, energy: 50, awakeIdle: idle);
        Assert.Equal(penalized ? 3 : 1, HappinessManager.ComputeHourlyDecay(pet));
    }

    // ── 衰減結算時機（每 3600 秒執行時間）──────────────────────────
    [Fact]
    public void Decay_is_settled_only_every_hour()
    {
        var hm = new HappinessManager();
        var pet = PetWith(happiness: 50);

        for (int i = 0; i < HappinessManager.DecaySettleIntervalSeconds - 1; i++)
            Assert.False(hm.Tick(pet).Settled);
        Assert.Equal(50, pet.Happiness); // 尚未滿一小時，不變

        var r = hm.Tick(pet); // 第 3600 秒
        Assert.True(r.Settled);
        Assert.Equal(1, r.DecayRate);
        Assert.Equal(-1, r.Delta);
        Assert.Equal(49, pet.Happiness);
        Assert.Equal(0, hm.DecayAccumSeconds);
    }

    [Fact]
    public void Decay_is_clamped_at_zero()
    {
        var hm = new HappinessManager();
        var pet = PetWith(happiness: 0, hunger: 71, energy: 19, awakeIdle: 4 * 3600 + 1); // 最壞 -5

        var r = hm.Tick2Hours(pet);
        Assert.Equal(0, pet.Happiness); // 夾在 0，不變負
    }

    [Fact]
    public void Decay_does_not_touch_hunger_energy_health()
    {
        var hm = new HappinessManager();
        var pet = new Pet { Happiness = 50, Hunger = 80, Energy = 10, Health = 60, AwakeIdleSeconds = 99999 };

        Tick(hm, pet, HappinessManager.DecaySettleIntervalSeconds * 2);
        Assert.Equal(80, pet.Hunger);
        Assert.Equal(10, pet.Energy);
        Assert.Equal(60, pet.Health); // 只動 Happiness
    }

    // ── §7.4.3 餵食回補 + 30 分冷卻 ───────────────────────────────
    [Fact]
    public void Feed_awards_ten_then_respects_thirty_minute_cooldown()
    {
        var hm = new HappinessManager();
        var pet = PetWith(happiness: 50);

        Assert.True(hm.TryAwardFeed(pet, T0));          // 首次：發放
        Assert.Equal(60, pet.Happiness);

        Assert.False(hm.TryAwardFeed(pet, T0.AddMinutes(29))); // 冷卻中
        Assert.Equal(60, pet.Happiness);                       // 未加

        Assert.True(hm.TryAwardFeed(pet, T0.AddMinutes(30)));   // 滿 30 分：再發
        Assert.Equal(70, pet.Happiness);
    }

    [Fact]
    public void Feed_award_is_clamped_at_100()
    {
        var hm = new HappinessManager();
        var pet = PetWith(happiness: 95);

        Assert.True(hm.TryAwardFeed(pet, T0)); // 冷卻已過即回傳 true
        Assert.Equal(100, pet.Happiness);      // +10 夾在 100
    }

    // ── 點擊 / 玩耍 +2 / 60 秒冷卻 ────────────────────────────────
    [Fact]
    public void Click_awards_two_then_respects_sixty_second_cooldown()
    {
        var hm = new HappinessManager();
        var pet = PetWith(happiness: 50);

        Assert.True(hm.TryAwardClickOrPlay(pet, T0));
        Assert.Equal(52, pet.Happiness);

        Assert.False(hm.TryAwardClickOrPlay(pet, T0.AddSeconds(59))); // 冷卻中
        Assert.Equal(52, pet.Happiness);

        Assert.True(hm.TryAwardClickOrPlay(pet, T0.AddSeconds(60)));  // 滿 60 秒
        Assert.Equal(54, pet.Happiness);
    }

    // ── 睡眠完成 +5 / 無冷卻（可連續）─────────────────────────────
    [Fact]
    public void SleepComplete_awards_five_without_cooldown()
    {
        var hm = new HappinessManager();
        var pet = PetWith(happiness: 50);

        Assert.Equal(5, hm.AwardSleepComplete(pet));
        Assert.Equal(5, hm.AwardSleepComplete(pet)); // 無冷卻，可再次
        Assert.Equal(60, pet.Happiness);
    }

    [Fact]
    public void SleepComplete_is_clamped_at_100()
    {
        var hm = new HappinessManager();
        var pet = PetWith(happiness: 98);
        Assert.Equal(2, hm.AwardSleepComplete(pet)); // 98→100，實際 +2
        Assert.Equal(0, hm.AwardSleepComplete(pet)); // 已滿，+0
        Assert.Equal(100, pet.Happiness);
    }

    // ── 雙寵物互動 +3 / 30 分冷卻 ─────────────────────────────────
    [Fact]
    public void PetInteraction_awards_three_then_respects_thirty_minute_cooldown()
    {
        var hm = new HappinessManager();
        var pet = PetWith(happiness: 50);

        Assert.True(hm.TryAwardPetInteraction(pet, T0));
        Assert.Equal(53, pet.Happiness);

        Assert.False(hm.TryAwardPetInteraction(pet, T0.AddMinutes(29)));
        Assert.True(hm.TryAwardPetInteraction(pet, T0.AddMinutes(30)));
        Assert.Equal(56, pet.Happiness);
    }

    // ── 點擊與互動共用 LastInteractionTime（刻意耦合，§7.4.6）────────
    [Fact]
    public void Click_and_interaction_share_the_same_cooldown_anchor()
    {
        var hm = new HappinessManager();
        var pet = PetWith(happiness: 50);

        Assert.True(hm.TryAwardClickOrPlay(pet, T0));              // LastInteractionTime = T0
        // 互動冷卻 30 分：點擊後 5 分內互動被擋
        Assert.False(hm.TryAwardPetInteraction(pet, T0.AddMinutes(5)));
        // 但點擊冷卻只 60 秒：點擊後 2 分可再點擊，且會把錨點推到 T0+2m
        Assert.True(hm.TryAwardClickOrPlay(pet, T0.AddMinutes(2)));
        // 此時互動需距 T0+2m 滿 30 分
        Assert.False(hm.TryAwardPetInteraction(pet, T0.AddMinutes(31)));
        Assert.True(hm.TryAwardPetInteraction(pet, T0.AddMinutes(32)));
    }

    [Fact]
    public void Feed_cooldown_is_independent_from_interaction_cooldown()
    {
        var hm = new HappinessManager();
        var pet = PetWith(happiness: 50);

        Assert.True(hm.TryAwardFeed(pet, T0));            // LastFedTime = T0
        Assert.True(hm.TryAwardClickOrPlay(pet, T0));     // 點擊用另一欄位，不受餵食影響
        Assert.Equal(62, pet.Happiness);                  // +10 +2
    }

    // ── 冷卻只 gate 幸福度，不動 AwakeIdleSeconds（歸零是互動層的事）──
    [Fact]
    public void Awards_do_not_reset_AwakeIdleSeconds()
    {
        var hm = new HappinessManager();
        var pet = PetWith(happiness: 50, awakeIdle: 5000);

        hm.TryAwardFeed(pet, T0);
        hm.TryAwardClickOrPlay(pet, T0);
        hm.AwardSleepComplete(pet);
        Assert.Equal(5000, pet.AwakeIdleSeconds); // 歸零由 D3 負責，本類別不碰
    }

    // ── 防呆 ──────────────────────────────────────────────────────
    [Fact]
    public void Null_pet_throws()
    {
        var hm = new HappinessManager();
        Assert.Throws<ArgumentNullException>(() => hm.Tick(null!));
        Assert.Throws<ArgumentNullException>(() => hm.TryAwardFeed(null!, T0));
        Assert.Throws<ArgumentNullException>(() => hm.AwardSleepComplete(null!));
        Assert.Throws<ArgumentNullException>(() => HappinessManager.ComputeHourlyDecay(null!));
    }

    [Fact]
    public void Constants_match_design_doc()
    {
        Assert.Equal(3600, HappinessManager.DecaySettleIntervalSeconds);
        Assert.Equal(10, HappinessManager.FeedHappiness);
        Assert.Equal(2, HappinessManager.ClickHappiness);
        Assert.Equal(5, HappinessManager.SleepHappiness);
        Assert.Equal(3, HappinessManager.InteractionHappiness);
        Assert.Equal(TimeSpan.FromMinutes(30), HappinessManager.FeedCooldown);
        Assert.Equal(TimeSpan.FromSeconds(60), HappinessManager.ClickCooldown);
        Assert.Equal(TimeSpan.FromMinutes(30), HappinessManager.InteractionCooldown);
    }
}

internal static class HappinessManagerTestExtensions
{
    /// <summary>連續 tick 兩個結算週期，供衰減夾值測試。</summary>
    public static Pet Tick2Hours(this HappinessManager hm, Pet pet)
    {
        for (int i = 0; i < HappinessManager.DecaySettleIntervalSeconds * 2; i++) hm.Tick(pet);
        return pet;
    }
}
