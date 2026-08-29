using DesktopPet.Models;
using DesktopPet.Utils;
using Xunit;

namespace DesktopPet.Tests;

/// <summary>
/// 驗證 <see cref="StorageManager"/> 的存檔讀寫、備份輪替與損毀復原（設計檔 §8）。
/// 每個測試使用獨立的暫存目錄，避免相互污染，也不觸及 %APPDATA%。
/// </summary>
public sealed class StorageManagerTests : IDisposable
{
    private readonly string _dir;

    public StorageManagerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "DesktopPetTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch
        {
            // 清理失敗不影響測試結果。
        }
    }

    private StorageManager NewManager() => new(_dir);

    [Fact]
    public void Load_on_empty_directory_returns_defaults()
    {
        using var storage = NewManager();

        var state = storage.Load();

        Assert.NotNull(state);
        Assert.Empty(state.Pets);
        Assert.Equal(2, state.MaxPetSlots);
        Assert.Equal("zh-TW", state.Settings.CurrentLanguage);
        Assert.Empty(state.Achievements);
    }

    [Fact]
    public void Save_then_Load_round_trips_all_sections()
    {
        using var storage = NewManager();

        var state = new GameState
        {
            MaxPetSlots = 2,
            Pets =
            {
                new Pet
                {
                    Id = "pet_001",
                    Name = "Fluffy",
                    Hunger = 50,
                    Energy = 70,
                    Happiness = 80,
                    Health = 100,
                    CurrentMood = PetMood.LowEnergy,
                    Sounds = new PetSoundSet { FeedSoundPath = "custom_sounds/pet_001_feed.mp3" },
                },
            },
        };
        state.Settings.CurrentLanguage = "ja-JP";
        state.Settings.Audio.BackgroundMusicPaths.Add("custom_sounds/bgm.mp3");
        state.Achievements["first_feed"] = 1;

        storage.Save(state);
        var loaded = storage.Load();

        var pet = Assert.Single(loaded.Pets);
        Assert.Equal("Fluffy", pet.Name);
        Assert.Equal(50, pet.Hunger);
        Assert.Equal(PetMood.LowEnergy, pet.CurrentMood);
        Assert.Equal("custom_sounds/pet_001_feed.mp3", pet.Sounds.FeedSoundPath);
        Assert.Null(pet.Sounds.ClickSoundPath); // null 代表使用預設音效
        Assert.Equal(2, loaded.MaxPetSlots);
        Assert.Equal("ja-JP", loaded.Settings.CurrentLanguage);
        Assert.Contains("custom_sounds/bgm.mp3", loaded.Settings.Audio.BackgroundMusicPaths);
        Assert.Equal(1, loaded.Achievements["first_feed"]);
    }

    [Fact]
    public void Save_writes_the_three_files_and_stores_mood_as_string()
    {
        using var storage = NewManager();
        var state = new GameState { Pets = { new Pet { Id = "p", CurrentMood = PetMood.LowEnergy } } };

        storage.Save(state);

        Assert.True(File.Exists(Path.Combine(_dir, "pet_data.json")));
        Assert.True(File.Exists(Path.Combine(_dir, "settings.json")));
        Assert.True(File.Exists(Path.Combine(_dir, "achievements.json")));

        var raw = File.ReadAllText(Path.Combine(_dir, "pet_data.json"));
        Assert.Contains("LOW_ENERGY", raw);          // 列舉以字串存檔
        Assert.DoesNotContain("\"currentMood\": 1", raw); // 不得存成數字
    }

    [Fact]
    public void Repeated_saves_rotate_backups_and_keep_at_most_three()
    {
        using var storage = NewManager();
        var petData = Path.Combine(_dir, "pet_data.json");

        // 第 1 次寫入：尚無備份
        storage.Save(new GameState());
        Assert.False(File.Exists(petData + ".bak1"));

        // 第 2 次：產生 .bak1
        storage.Save(new GameState());
        Assert.True(File.Exists(petData + ".bak1"));

        // 再寫數次：最多保留 .bak1..bak3，不產生 .bak4
        storage.Save(new GameState());
        storage.Save(new GameState());
        storage.Save(new GameState());

        Assert.True(File.Exists(petData + ".bak1"));
        Assert.True(File.Exists(petData + ".bak2"));
        Assert.True(File.Exists(petData + ".bak3"));
        Assert.False(File.Exists(petData + ".bak4"));
    }

    [Fact]
    public void Load_recovers_from_backup_when_primary_is_corrupt()
    {
        using var storage = NewManager();
        var petData = Path.Combine(_dir, "pet_data.json");

        // save#1（之後會成為 .bak1）
        storage.Save(new GameState { Pets = { new Pet { Id = "good", Name = "Good" } } });
        // save#2（成為當前主檔）
        storage.Save(new GameState { Pets = { new Pet { Id = "newer", Name = "Newer" } } });

        // 損毀主檔
        File.WriteAllText(petData, "{ this is not valid json ]");

        var loaded = storage.Load();

        // 主檔壞掉 → 自 .bak1（save#1）復原
        var pet = Assert.Single(loaded.Pets);
        Assert.Equal("Good", pet.Name);
    }

    [Fact]
    public void AutoSaveInterval_is_five_minutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(5), StorageManager.AutoSaveInterval);
    }
}
