using DesktopPet.Core;
using DesktopPet.Models;
using Xunit;

namespace DesktopPet.Tests;

/// <summary>
/// 驗證 E4 照顧動作的數值效果（<see cref="PetCareActions"/>）：餵食降低飢餓度、睡眠回補能量，
/// 皆夾在 0–100，且只碰 <c>Hunger</c>/<c>Energy</c>（幸福度由 <c>HappinessManager</c> 負責）。
/// </summary>
public class PetCareActionsTests
{
    // ── 餵食：Hunger -30，夾 0 ────────────────────────────────────
    [Fact]
    public void Feed_reduces_hunger_by_relief_amount()
    {
        var pet = new Pet { Hunger = 80, Energy = 50, Happiness = 50, Health = 50 };
        int delta = PetCareActions.Feed(pet);

        Assert.Equal(80 - PetCareActions.FeedHungerRelief, pet.Hunger);
        Assert.Equal(-PetCareActions.FeedHungerRelief, delta);
        // 不碰其他數值。
        Assert.Equal(50, pet.Energy);
        Assert.Equal(50, pet.Happiness);
        Assert.Equal(50, pet.Health);
    }

    [Fact]
    public void Feed_clamps_hunger_at_zero()
    {
        var pet = new Pet { Hunger = 10 };
        int delta = PetCareActions.Feed(pet);

        Assert.Equal(0, pet.Hunger);
        Assert.Equal(-10, delta); // 只實際降了 10（夾住）。
    }

    [Fact]
    public void Feed_on_zero_hunger_is_noop()
    {
        var pet = new Pet { Hunger = 0 };
        Assert.Equal(0, PetCareActions.Feed(pet));
        Assert.Equal(0, pet.Hunger);
    }

    // ── 睡眠：每 tick Energy +1，回滿即回報醒來 ─────────────────────
    [Fact]
    public void RecoverEnergyForSleep_increments_and_reports_not_full()
    {
        var pet = new Pet { Energy = 50 };
        bool full = PetCareActions.RecoverEnergyForSleep(pet);

        Assert.Equal(51, pet.Energy);
        Assert.False(full);
    }

    [Fact]
    public void RecoverEnergyForSleep_reports_full_at_hundred()
    {
        var pet = new Pet { Energy = 99 };
        bool full = PetCareActions.RecoverEnergyForSleep(pet);

        Assert.Equal(100, pet.Energy);
        Assert.True(full);
    }

    [Fact]
    public void RecoverEnergyForSleep_clamps_at_hundred()
    {
        var pet = new Pet { Energy = 100 };
        bool full = PetCareActions.RecoverEnergyForSleep(pet);

        Assert.Equal(100, pet.Energy); // 不超過 100。
        Assert.True(full);
    }

    [Fact]
    public void RecoverEnergyForSleep_from_empty_fills_within_hundred_ticks()
    {
        var pet = new Pet { Energy = 0 };
        int ticks = 0;
        while (!PetCareActions.RecoverEnergyForSleep(pet))
        {
            ticks++;
            Assert.True(ticks < 200, "睡眠回補不應無限循環。");
        }

        Assert.Equal(100, pet.Energy);
        Assert.Equal(99, ticks); // 0→100 共 100 次 +1，最後一次回報 full。
    }
}
