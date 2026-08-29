using System.IO;
using System.Linq;
using DesktopPet.Core.Visuals;
using Xunit;

namespace DesktopPet.Tests;

/// <summary>
/// 驗證設計檔 §7.3.3：pet_visuals.json 解析（含 UPPER_SNAKE 代號↔<see cref="PetVisualState"/>、
/// 心情代號與前綴非一對一）、缺檔／破損退回標準 6 類型、掃描資料夾建單元索引（依序號排序、
/// 不要求連號、略過非素材檔）。皆為純邏輯，跨平台可跑。
/// </summary>
public class VisualRegistryTests : IDisposable
{
    private const string CanonicalJson = """
    {
      "visuals": [
        { "code": "SAD",        "kind": "mood",  "prefix": "anim_sad",   "required": false, "fallback": "NEUTRAL",    "rerollIntervalSec": 0 },
        { "code": "LOW_ENERGY", "kind": "mood",  "prefix": "anim_tired", "required": false, "fallback": "NEUTRAL",    "rerollIntervalSec": 0 },
        { "code": "NEUTRAL",    "kind": "mood",  "prefix": "anim_idle",  "required": true,  "fallback": null,         "rerollIntervalSec": 8 },
        { "code": "CLICK",      "kind": "event", "prefix": "anim_click", "required": false, "fallback": null,         "durationSec": 1.5 },
        { "code": "FEED",       "kind": "event", "prefix": "anim_feed",  "required": false, "fallback": null,         "durationSec": 2.5 },
        { "code": "SLEEP",      "kind": "event", "prefix": "anim_sleep", "required": false, "fallback": "LOW_ENERGY", "durationSec": 0, "rerollIntervalSec": 20 }
      ],
      "weather": { "enabled": false, "weatherChance": 0.3, "pollIntervalMin": 30, "codes": ["clear","cloudy"] }
    }
    """;

    private readonly string _dir;

    public VisualRegistryTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "DesktopPetVisualsTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* 忽略清理錯誤 */ }
    }

    private void Touch(string fileName) => File.WriteAllText(Path.Combine(_dir, fileName), "x");

    // ── 解析 pet_visuals.json ─────────────────────────────────────
    [Fact]
    public void Parses_canonical_config_with_all_fields()
    {
        var reg = VisualRegistry.LoadFromJson(CanonicalJson);

        Assert.Equal(6, reg.Definitions.Count);

        var neutral = reg.GetDefinition(PetVisualState.Neutral)!;
        Assert.Equal(VisualKind.Mood, neutral.Kind);
        Assert.Equal("anim_idle", neutral.Prefix);
        Assert.True(neutral.Required);
        Assert.Null(neutral.Fallback);
        Assert.Equal(8, neutral.RerollIntervalSec);

        var lowEnergy = reg.GetDefinition(PetVisualState.LowEnergy)!;
        Assert.Equal("anim_tired", lowEnergy.Prefix);              // 代號與前綴非一對一
        Assert.Equal(PetVisualState.Neutral, lowEnergy.Fallback);

        var click = reg.GetDefinition(PetVisualState.Click)!;
        Assert.Equal(VisualKind.Event, click.Kind);
        Assert.Equal(1.5, click.DurationSec);
        Assert.Null(click.Fallback);

        var sleep = reg.GetDefinition(PetVisualState.Sleep)!;
        Assert.Equal(PetVisualState.LowEnergy, sleep.Fallback);
        Assert.Equal(20, sleep.RerollIntervalSec);
        Assert.Equal(0, sleep.DurationSec);                       // 0 = 持續型
    }

    [Fact]
    public void Code_round_trips_through_upper_snake()
    {
        Assert.Equal("LOW_ENERGY", VisualRegistry.ToCode(PetVisualState.LowEnergy));
        Assert.Equal("NEUTRAL", VisualRegistry.ToCode(PetVisualState.Neutral));
        Assert.Equal("CLICK", VisualRegistry.ToCode(PetVisualState.Click));
    }

    [Fact]
    public void Unknown_code_is_skipped_without_throwing()
    {
        var reg = VisualRegistry.LoadFromJson("""
        { "visuals": [
            { "code": "NEUTRAL", "kind": "mood", "prefix": "anim_idle", "required": true },
            { "code": "BOGUS",   "kind": "mood", "prefix": "anim_bogus" }
        ] }
        """);

        Assert.Single(reg.Definitions);
        Assert.True(reg.Contains(PetVisualState.Neutral));
    }

    // ── 缺檔／破損 → 標準 6 類型 ──────────────────────────────────
    [Fact]
    public void Missing_file_falls_back_to_default_definitions()
    {
        var reg = VisualRegistry.LoadFromFile(Path.Combine(_dir, "does_not_exist.json"));
        Assert.Equal(6, reg.Definitions.Count);
        Assert.Equal("anim_tired", reg.GetDefinition(PetVisualState.LowEnergy)!.Prefix);
    }

    [Fact]
    public void Corrupt_json_falls_back_to_default_definitions()
    {
        var reg = VisualRegistry.LoadFromJson("{ not valid ");
        Assert.Equal(6, reg.Definitions.Count);
    }

    [Fact]
    public void Default_definitions_match_the_spec_table()
    {
        var reg = VisualRegistry.FromDefinitions(VisualRegistry.DefaultDefinitions());
        Assert.True(reg.GetDefinition(PetVisualState.Neutral)!.Required);
        Assert.Equal(PetVisualState.LowEnergy, reg.GetDefinition(PetVisualState.Sleep)!.Fallback);
        Assert.Equal(2.5, reg.GetDefinition(PetVisualState.Feed)!.DurationSec);
    }

    // ── 掃描資料夾建單元索引 ──────────────────────────────────────
    [Fact]
    public void Scan_indexes_units_per_state_sorted_by_number()
    {
        Touch("anim_idle_1.png");
        Touch("anim_idle_3.png");   // 不要求連號（缺 2）
        Touch("anim_idle_2.jpg");   // 允許 jpg
        Touch("anim_tired_1.png");
        Touch("anim_sad_1.PNG");    // 副檔名大小寫不敏感
        Touch("interaction_greet.png");  // 互動素材，不屬任何 anim_ 前綴
        Touch("skin.json");         // 描述檔
        Touch("notes.txt");         // 雜檔

        var reg = VisualRegistry.LoadFromJson(CanonicalJson);
        var pool = reg.ScanUnits(_dir);

        Assert.Equal(new[] { "anim_idle_1", "anim_idle_2", "anim_idle_3" }, pool[PetVisualState.Neutral]);
        Assert.Equal(new[] { "anim_tired_1" }, pool[PetVisualState.LowEnergy]);
        Assert.Equal(new[] { "anim_sad_1" }, pool[PetVisualState.Sad]);

        // 沒有 click/feed/sleep 檔 → 這些狀態不在池內
        Assert.False(pool.ContainsKey(PetVisualState.Click));
        Assert.False(pool.ContainsKey(PetVisualState.Sleep));
    }

    [Fact]
    public void Scan_does_not_match_prefix_with_extra_suffix()
    {
        Touch("anim_idle_1.png");
        Touch("anim_idle_1_extra.png");  // 不符 {prefix}_{數字}
        Touch("anim_idlex_1.png");       // 前綴不同

        var pool = VisualRegistry.LoadFromJson(CanonicalJson).ScanUnits(_dir);
        Assert.Equal(new[] { "anim_idle_1" }, pool[PetVisualState.Neutral]);
    }

    [Fact]
    public void Scan_missing_folder_returns_empty()
    {
        var pool = VisualRegistry.LoadFromJson(CanonicalJson).ScanUnits(Path.Combine(_dir, "nope"));
        Assert.Empty(pool);
    }
}
