using System;
using System.IO;
using DesktopPet.Core;
using DesktopPet.Models;
using Xunit;

namespace DesktopPet.Tests;

/// <summary>
/// 驗證 E2 新寵物工廠（<see cref="PetFactory"/>）：起始數值、內建圖樣分配（第 1 隻貓、第 2 隻狗）、
/// 數量夾在 1–2、Id 唯一、圖樣路徑正確組合。
/// </summary>
public class PetFactoryTests
{
    private static readonly DateTime T0 = new(2026, 9, 7, 10, 0, 0, DateTimeKind.Local);
    private const string ThemesDir = "/themes";

    [Fact]
    public void Create_applies_starting_values_and_skin()
    {
        var pet = PetFactory.Create("小貓", PetFactory.BuiltinCatSkinId, ThemesDir, T0);

        Assert.Equal(PetFactory.StartHunger, pet.Hunger);
        Assert.Equal(PetFactory.StartEnergy, pet.Energy);
        Assert.Equal(PetFactory.StartHappiness, pet.Happiness);
        Assert.Equal(PetFactory.StartHealth, pet.Health);
        Assert.Equal(PetFactory.StartLevel, pet.Level);
        Assert.Equal(PetMood.Neutral, pet.CurrentMood);
        Assert.Equal(T0, pet.CreatedDate);
        Assert.Equal(T0, pet.LastTickTime);
        Assert.Equal(PetFactory.BuiltinSourceType, pet.SkinSourceType);
        Assert.Equal(PetFactory.BuiltinCatSkinId, pet.SkinId);
        Assert.Equal(Path.Combine(ThemesDir, PetFactory.BuiltinCatSkinId), pet.SkinFolderPath);
        Assert.False(string.IsNullOrEmpty(pet.Id));
    }

    [Fact]
    public void StartingValues_do_not_start_in_sad_or_low_energy()
    {
        // §7.2.1：起始不該落進 SAD（Hunger>70）或 LOW_ENERGY（Energy<20）。
        Assert.True(PetFactory.StartHunger <= 70);
        Assert.True(PetFactory.StartEnergy >= 20);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(0, 1)]   // 夾回下限
    [InlineData(5, 2)]   // 夾回上限（僅兩套內建主題）
    public void CreateInitialPets_clamps_count(int requested, int expected)
    {
        var pets = PetFactory.CreateInitialPets(requested, ThemesDir, T0);
        Assert.Equal(expected, pets.Count);
    }

    [Fact]
    public void CreateInitialPets_two_assigns_cat_then_dog_with_unique_ids()
    {
        var pets = PetFactory.CreateInitialPets(2, ThemesDir, T0);

        Assert.Equal(PetFactory.BuiltinCatSkinId, pets[0].SkinId);
        Assert.Equal(PetFactory.BuiltinDogSkinId, pets[1].SkinId);
        Assert.NotEqual(pets[0].Id, pets[1].Id);
    }
}
