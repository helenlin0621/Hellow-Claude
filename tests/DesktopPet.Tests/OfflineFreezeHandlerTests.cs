using System;
using System.Collections.Generic;
using DesktopPet.Core;
using DesktopPet.Models;
using Xunit;

namespace DesktopPet.Tests;

/// <summary>
/// 驗證設計檔 §7.4.4 離線凍結的關鍵不變量：四項數值與兩個累計欄位<b>全部不變</b>，
/// 唯一副作用是把 <see cref="Pet.LastTickTime"/> 重設為現在時刻。寫錯（例如補算離線變化、
/// 或把 <c>AwakeIdleSeconds</c> 歸零 / 隨離線時間累加）會讓凍結形同虛設。
/// </summary>
public class OfflineFreezeHandlerTests
{
    private static Pet FullyPopulatedPet() => new()
    {
        Hunger = 65,
        Energy = 40,
        Happiness = 88,
        Health = 73,
        AwakeIdleSeconds = 5000,
        HealthCheckSeconds = 1200,
        LastTickTime = new DateTime(2026, 8, 20, 8, 0, 0, DateTimeKind.Local), // 關閉時的舊時刻
        CurrentMood = PetMood.Sad,
    };

    [Fact]
    public void Apply_freezes_all_four_values_and_both_counters()
    {
        var pet = FullyPopulatedPet();
        var now = new DateTime(2026, 8, 29, 9, 30, 0, DateTimeKind.Local);

        new OfflineFreezeHandler().Apply(pet, now);

        // 四項數值不變（不做任何離線補算）
        Assert.Equal(65, pet.Hunger);
        Assert.Equal(40, pet.Energy);
        Assert.Equal(88, pet.Happiness);
        Assert.Equal(73, pet.Health);
        // 兩個累計欄位不變（不因離線時間增加、也不歸零）
        Assert.Equal(5000, pet.AwakeIdleSeconds);
        Assert.Equal(1200, pet.HealthCheckSeconds);
    }

    [Fact]
    public void Apply_resets_only_LastTickTime()
    {
        var pet = FullyPopulatedPet();
        var now = new DateTime(2026, 8, 29, 9, 30, 0, DateTimeKind.Local);

        new OfflineFreezeHandler().Apply(pet, now);

        Assert.Equal(now, pet.LastTickTime); // 唯一副作用
    }

    [Fact]
    public void Apply_uses_injected_clock_when_now_omitted()
    {
        var stamp = new DateTime(2026, 8, 29, 12, 0, 0, DateTimeKind.Local);
        var pet = FullyPopulatedPet();

        new OfflineFreezeHandler(() => stamp).Apply(pet);

        Assert.Equal(stamp, pet.LastTickTime);
    }

    [Fact]
    public void Apply_to_collection_uses_same_now_for_all_pets()
    {
        var stamp = new DateTime(2026, 8, 29, 15, 45, 0, DateTimeKind.Local);
        var pets = new List<Pet> { FullyPopulatedPet(), FullyPopulatedPet() };

        new OfflineFreezeHandler(() => stamp).Apply(pets);

        Assert.All(pets, p => Assert.Equal(stamp, p.LastTickTime));
    }

    [Fact]
    public void Apply_null_pet_throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new OfflineFreezeHandler().Apply((Pet)null!, DateTime.Now));
    }

    [Fact]
    public void Apply_null_collection_throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new OfflineFreezeHandler().Apply((IEnumerable<Pet>)null!));
    }
}
