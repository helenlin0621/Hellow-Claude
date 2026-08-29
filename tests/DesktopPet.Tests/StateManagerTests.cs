using System;
using DesktopPet.Core;
using DesktopPet.Models;
using Xunit;

namespace DesktopPet.Tests;

/// <summary>
/// 驗證設計檔 §7.4.1 / §7.4.5 狀態結算的關鍵不變量：
/// <c>Hunger</c> 每 3 分鐘 <b>+1（遞增，越高越餓）</b>、<c>Energy</c> 每 5 分鐘 -1、
/// <c>AwakeIdleSeconds</c> / <c>HealthCheckSeconds</c> 每秒累加、健康度每 30 分鐘依三分支結算、
/// 四項夾在 0–100、且<b>完全不碰 <c>Happiness</c></b>（僅結算時唯讀取用）。
/// 這些寫錯會讓心情分支永不觸發、凍結失效或健康度亂跳。
/// </summary>
public class StateManagerTests
{
    private static Pet PetWith(int hunger = 50, int energy = 50, int happiness = 50, int health = 50) =>
        new() { Hunger = hunger, Energy = energy, Happiness = happiness, Health = health };

    private static void Tick(StateManager sm, Pet pet, int times)
    {
        for (int i = 0; i < times; i++) sm.Tick(pet);
    }

    // ── 飢餓度：每 3 分鐘 +1，且是「遞增」──────────────────────────
    [Fact]
    public void Hunger_increases_by_one_exactly_at_three_minute_boundary()
    {
        var sm = new StateManager();
        var pet = PetWith(hunger: 50);

        Tick(sm, pet, StateManager.HungerIntervalSeconds - 1); // 179 秒
        Assert.Equal(50, pet.Hunger);                          // 尚未滿一週期，不變

        var r = sm.Tick(pet);                                  // 第 180 秒
        Assert.Equal(51, pet.Hunger);                          // 遞增（越高越餓）
        Assert.Equal(+1, r.HungerDelta);
        Assert.Equal(0, sm.HungerAccumSeconds);                // 零頭歸零
    }

    [Fact]
    public void Hunger_accumulates_across_multiple_periods()
    {
        var sm = new StateManager();
        var pet = PetWith(hunger: 0);

        Tick(sm, pet, StateManager.HungerIntervalSeconds * 5); // 5 個週期
        Assert.Equal(5, pet.Hunger);
    }

    [Fact]
    public void Hunger_is_clamped_at_max_100()
    {
        var sm = new StateManager();
        var pet = PetWith(hunger: 100);

        Tick(sm, pet, StateManager.HungerIntervalSeconds - 1); // 逼近週期邊界
        var r = sm.Tick(pet);                                  // 跨越週期的那一 tick
        Assert.Equal(100, pet.Hunger);                         // 夾在 100，不溢出
        Assert.Equal(0, r.HungerDelta);                        // 已達上限，實際不變
    }

    // ── 能量：每 5 分鐘 -1 ─────────────────────────────────────────
    [Fact]
    public void Energy_decreases_by_one_exactly_at_five_minute_boundary()
    {
        var sm = new StateManager();
        var pet = PetWith(energy: 50);

        Tick(sm, pet, StateManager.EnergyIntervalSeconds - 1); // 299 秒
        Assert.Equal(50, pet.Energy);

        var r = sm.Tick(pet); // 第 300 秒
        Assert.Equal(49, pet.Energy);
        Assert.Equal(-1, r.EnergyDelta);
        Assert.Equal(0, sm.EnergyAccumSeconds);
    }

    [Fact]
    public void Energy_is_clamped_at_min_0()
    {
        var sm = new StateManager();
        var pet = PetWith(energy: 0);

        Tick(sm, pet, StateManager.EnergyIntervalSeconds); // 一整個週期
        Assert.Equal(0, pet.Energy); // 夾在 0，不變負
    }

    [Fact]
    public void Hunger_and_energy_advance_independently()
    {
        var sm = new StateManager();
        var pet = PetWith(hunger: 0, energy: 100);

        // 900 秒 = 飢餓 5 週期(+5)、能量 3 週期(-3)
        Tick(sm, pet, 900);
        Assert.Equal(5, pet.Hunger);
        Assert.Equal(97, pet.Energy);
    }

    // ── 累計計時：每秒 +1 ──────────────────────────────────────────
    [Fact]
    public void AwakeIdleSeconds_and_HealthCheckSeconds_accumulate_each_tick()
    {
        var sm = new StateManager();
        var pet = PetWith();

        Tick(sm, pet, 42);
        Assert.Equal(42, pet.AwakeIdleSeconds);
        Assert.Equal(42, pet.HealthCheckSeconds);
    }

    [Fact]
    public void StateManager_does_not_reset_AwakeIdleSeconds()
    {
        // 歸零是互動層（D3/E4）的責任；本類別只累加，且從既有值續累。
        var sm = new StateManager();
        var pet = PetWith();
        pet.AwakeIdleSeconds = 1000;

        Tick(sm, pet, 3);
        Assert.Equal(1003, pet.AwakeIdleSeconds);
    }

    // ── 健康度：每 30 分鐘結算（§7.4.5 三分支）─────────────────────
    [Fact]
    public void Health_is_settled_only_every_thirty_minutes()
    {
        var sm = new StateManager();
        var pet = PetWith(hunger: 95, health: 50); // Hunger>90 → 惡劣條件

        for (int i = 0; i < StateManager.HealthCheckIntervalSeconds - 1; i++)
            Assert.False(sm.Tick(pet).HealthSettled); // 尚未滿 30 分，不結算

        var r = sm.Tick(pet); // 第 1800 秒
        Assert.True(r.HealthSettled);
        Assert.Equal(0, pet.HealthCheckSeconds); // 計時歸零
    }

    [Theory]
    // 惡劣條件（任一成立）→ -1
    [InlineData(91, 50, 50, -1)]  // Hunger > 90
    [InlineData(50, 9, 50, -1)]   // Energy < 10
    [InlineData(50, 50, 19, -1)]  // Happiness < 20
    // 良好條件（三者皆成立）→ +1
    [InlineData(29, 71, 71, +1)]
    // 一般狀態 → 不變
    [InlineData(50, 50, 50, 0)]
    [InlineData(90, 10, 20, 0)]   // 邊界值皆不觸發（嚴格比較）
    public void Health_settlement_follows_three_branch_rule(int hunger, int energy, int happiness, int expectedDelta)
    {
        var sm = new StateManager();
        var pet = PetWith(hunger: hunger, energy: energy, happiness: happiness, health: 50);
        pet.HealthCheckSeconds = StateManager.HealthCheckIntervalSeconds - 1; // 下一 tick 即結算

        var r = sm.Tick(pet);

        Assert.True(r.HealthSettled);
        Assert.Equal(expectedDelta, r.HealthDelta);
        Assert.Equal(50 + expectedDelta, pet.Health);
    }

    [Fact]
    public void Health_drop_wins_when_a_single_bad_metric_coexists_with_good_ones()
    {
        // Hunger/Energy 看似良好（滿足 +1 的 Hunger<30、Energy>70），但 Happiness<20 觸發惡劣分支，
        // 因 -1 分支先判定（else if），任一項惡劣即扣血，不會被其他良好項翻成 +1。
        var sm = new StateManager();
        var pet = PetWith(hunger: 10, energy: 90, happiness: 10, health: 50);
        pet.HealthCheckSeconds = StateManager.HealthCheckIntervalSeconds - 1;

        Assert.Equal(-1, sm.Tick(pet).HealthDelta);
    }

    [Fact]
    public void Health_is_clamped_at_bounds_on_settlement()
    {
        var sm = new StateManager();
        var pet = PetWith(hunger: 95, health: 0); // 惡劣但已見底
        pet.HealthCheckSeconds = StateManager.HealthCheckIntervalSeconds - 1;

        var r = sm.Tick(pet);
        Assert.True(r.HealthSettled);
        Assert.Equal(0, r.HealthDelta); // 夾在 0，實際不變
        Assert.Equal(0, pet.Health);
    }

    // ── 不碰 Happiness（C2 的職責）─────────────────────────────────
    [Fact]
    public void StateManager_never_modifies_happiness()
    {
        var sm = new StateManager();
        var pet = PetWith(hunger: 0, energy: 100, happiness: 42, health: 50);

        Tick(sm, pet, StateManager.HealthCheckIntervalSeconds * 2); // 跨越多次各種結算
        Assert.Equal(42, pet.Happiness); // 幸福度衰減 / 回補由 C2 負責，此處不動
    }

    // ── LastTickTime 更新為時鐘值 ──────────────────────────────────
    [Fact]
    public void Tick_updates_LastTickTime_to_clock()
    {
        var stamp = new DateTime(2026, 8, 29, 10, 0, 0, DateTimeKind.Local);
        var sm = new StateManager(() => stamp);
        var pet = PetWith();

        sm.Tick(pet);
        Assert.Equal(stamp, pet.LastTickTime);
    }

    // ── 防呆 ──────────────────────────────────────────────────────
    [Fact]
    public void Tick_null_pet_throws()
    {
        Assert.Throws<ArgumentNullException>(() => new StateManager().Tick(null!));
    }

    [Fact]
    public void Intervals_match_design_doc()
    {
        Assert.Equal(180, StateManager.HungerIntervalSeconds);     // 3 分鐘
        Assert.Equal(300, StateManager.EnergyIntervalSeconds);     // 5 分鐘
        Assert.Equal(1800, StateManager.HealthCheckIntervalSeconds); // 30 分鐘
    }
}
