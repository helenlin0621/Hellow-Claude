using System.Text.Json;
using System.Text.Json.Nodes;
using DesktopPet.Models;
using DesktopPet.Utils;
using Xunit;

namespace DesktopPet.Tests;

/// <summary>
/// 驗證設計檔 §4 的關鍵不變量：列舉以字串序列化（<c>LowEnergy → "LOW_ENERGY"</c>）、
/// 屬性 camelCase、<c>null</c> 音效路徑照寫。寫錯這些會導致舊存檔讀不回來（§4 已知限制）。
/// 測試對象為 <see cref="StorageManager.JsonOptions"/> —— 與正式存檔完全相同的序列化選項。
/// </summary>
public class SerializationTests
{
    private static readonly JsonSerializerOptions Options = StorageManager.JsonOptions;

    [Theory]
    [InlineData(PetMood.Sad, "SAD")]
    [InlineData(PetMood.LowEnergy, "LOW_ENERGY")]
    [InlineData(PetMood.Neutral, "NEUTRAL")]
    public void PetMood_serializes_to_upper_snake_case(PetMood mood, string expected)
    {
        var json = JsonSerializer.Serialize(mood, Options);
        Assert.Equal($"\"{expected}\"", json);
    }

    [Fact]
    public void PetMood_round_trips_through_string()
    {
        var json = JsonSerializer.Serialize(PetMood.LowEnergy, Options);
        Assert.Equal(PetMood.LowEnergy, JsonSerializer.Deserialize<PetMood>(json, Options));
    }

    [Theory]
    [InlineData("\"SAD\"", PetMood.Sad)]
    [InlineData("\"LOW_ENERGY\"", PetMood.LowEnergy)]
    [InlineData("\"NEUTRAL\"", PetMood.Neutral)]
    public void Deserializes_upper_snake_case_mood_from_saved_string(string json, PetMood expected)
    {
        // 模擬 §5.2 存檔中的字串值，確保舊存檔能讀回。
        Assert.Equal(expected, JsonSerializer.Deserialize<PetMood>(json, Options));
    }

    [Fact]
    public void Pet_uses_camelCase_property_names_and_string_mood()
    {
        var pet = new Pet { Id = "pet_001", Name = "Fluffy", CurrentMood = PetMood.LowEnergy };
        var node = JsonNode.Parse(JsonSerializer.Serialize(pet, Options))!.AsObject();

        Assert.Equal("pet_001", (string?)node["id"]);
        Assert.Equal("LOW_ENERGY", (string?)node["currentMood"]);
        Assert.True(node.ContainsKey("skinFolderPath"));
        Assert.False(node.ContainsKey("CurrentMood")); // 不得為 PascalCase
    }

    [Fact]
    public void Settings_groups_audio_under_nested_object_with_camelCase()
    {
        var settings = new Settings();
        settings.Audio.BackgroundMusicPaths.Add("custom_sounds/bgm.mp3");

        var node = JsonNode.Parse(JsonSerializer.Serialize(settings, Options))!.AsObject();

        Assert.True(node.ContainsKey("alwaysOnTop"));
        Assert.Equal("zh-TW", (string?)node["currentLanguage"]);

        var audio = node["audio"]!.AsObject();
        Assert.Equal(80, (int)audio["volume"]!);
        Assert.True(audio.ContainsKey("backgroundMusicPaths"));
    }

    [Fact]
    public void Null_sound_paths_are_written_not_omitted()
    {
        // §5.2：clickSoundPath = null 具語意（使用預設音效），不可被序列化省略。
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(new Pet(), Options));
        var sounds = doc.RootElement.GetProperty("sounds");

        Assert.True(sounds.TryGetProperty("clickSoundPath", out var click));
        Assert.Equal(JsonValueKind.Null, click.ValueKind);
    }
}
