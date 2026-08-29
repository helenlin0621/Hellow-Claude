using System.Text.Json;
using System.Text.Json.Nodes;
using DesktopPet.Core.Skins;
using Xunit;

namespace DesktopPet.Tests;

/// <summary>
/// 驗證設計檔 §6.4.5 的關鍵不變量：缺 <c>skin.json</c>（或單元未登記）一律視為 <c>frames: 1</c>；
/// 已登記單元回傳其格數描述；靜態單元序列化只留 <c>{ "frames": 1 }</c>；破損檔案不丟例外。
/// 寫錯這些會讓既有素材無法沿用，或讓 Sprite Sheet 凍結在第一格。
/// </summary>
public class SkinManifestTests : IDisposable
{
    private readonly string _dir;

    public SkinManifestTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "DesktopPetTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* 測試清理，忽略 */ }
    }

    private void WriteSkinJson(string json) => File.WriteAllText(Path.Combine(_dir, SkinManifest.FileName), json);

    // ── 缺檔 → 全視為單格 ─────────────────────────────────────────
    [Fact]
    public void Missing_skin_json_yields_single_frame_for_any_unit()
    {
        var manifest = SkinManifest.Load(_dir); // 資料夾內沒有 skin.json

        var unit = manifest.GetUnit("anim_idle_1");
        Assert.Equal("anim_idle_1", unit.Name);
        Assert.Equal(1, unit.Frames);
        Assert.False(unit.IsAnimated);
        Assert.False(unit.Loops);
        Assert.Null(unit.FrameWidth);
        Assert.Null(unit.Fps);
    }

    [Fact]
    public void Nonexistent_or_empty_path_does_not_throw()
    {
        Assert.Equal(1, SkinManifest.Load(Path.Combine(_dir, "nope")).GetUnit("x").Frames);
        Assert.Equal(1, SkinManifest.Load("").GetUnit("x").Frames);
    }

    // ── 已登記單元 → 回傳其描述 ────────────────────────────────────
    [Fact]
    public void Declared_units_return_their_frame_metadata()
    {
        WriteSkinJson("""
        {
          "schemaVersion": 2,
          "units": {
            "anim_idle_1": { "frames": 1 },
            "anim_idle_2": { "frames": 6, "frameWidth": 256, "fps": 12, "loop": true },
            "anim_click_1": { "frames": 8, "frameWidth": 256, "fps": 15, "loop": false }
          }
        }
        """);

        var manifest = SkinManifest.Load(_dir);

        var idle2 = manifest.GetUnit("anim_idle_2");
        Assert.Equal("anim_idle_2", idle2.Name);
        Assert.Equal(6, idle2.Frames);
        Assert.Equal(256, idle2.FrameWidth);
        Assert.Equal(12, idle2.Fps);
        Assert.True(idle2.Loops);
        Assert.True(idle2.IsAnimated);

        var click = manifest.GetUnit("anim_click_1");
        Assert.Equal(8, click.Frames);
        Assert.False(click.Loops); // loop:false → 停最後一格
    }

    [Fact]
    public void Unit_absent_from_existing_manifest_defaults_to_single_frame()
    {
        // 使用者丟了新的 anim_sad_1.png 卻沒更新 skin.json → 該單元視為單格（漸進補齊）
        WriteSkinJson("""{ "schemaVersion": 2, "units": { "anim_idle_1": { "frames": 4, "frameWidth": 256, "fps": 8, "loop": true } } }""");

        var sad = SkinManifest.Load(_dir).GetUnit("anim_sad_1");
        Assert.Equal("anim_sad_1", sad.Name);
        Assert.Equal(1, sad.Frames);
    }

    // ── 破損 / 非法值的韌性 ────────────────────────────────────────
    [Fact]
    public void Corrupt_json_falls_back_to_single_frame_without_throwing()
    {
        WriteSkinJson("{ this is not valid json ");
        var manifest = SkinManifest.Load(_dir);
        Assert.Equal(1, manifest.GetUnit("anim_idle_1").Frames);
    }

    [Fact]
    public void Frames_below_one_is_normalized_to_one()
    {
        WriteSkinJson("""{ "schemaVersion": 2, "units": { "anim_idle_1": { "frames": 0 } } }""");
        Assert.Equal(1, SkinManifest.Load(_dir).GetUnit("anim_idle_1").Frames);
    }

    // ── 存檔往返 + 序列化格式 ──────────────────────────────────────
    [Fact]
    public void Save_then_load_round_trips_units()
    {
        var manifest = new SkinManifest();
        manifest.Units["anim_idle_1"] = new VisualUnitInfo { Frames = 1 };
        manifest.Units["anim_idle_2"] = new VisualUnitInfo { Frames = 6, FrameWidth = 256, Fps = 12, Loop = true };

        manifest.Save(_dir);
        var reloaded = SkinManifest.Load(_dir);

        Assert.Equal(2, reloaded.SchemaVersion);
        Assert.Equal(1, reloaded.GetUnit("anim_idle_1").Frames);
        var idle2 = reloaded.GetUnit("anim_idle_2");
        Assert.Equal(6, idle2.Frames);
        Assert.Equal(256, idle2.FrameWidth);
        Assert.True(idle2.Loops);
    }

    [Fact]
    public void Static_unit_serializes_to_frames_only_with_camelCase()
    {
        var manifest = new SkinManifest();
        manifest.Units["anim_idle_1"] = new VisualUnitInfo { Frames = 1 };
        manifest.Save(_dir);

        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(_dir, SkinManifest.FileName)))!.AsObject();
        Assert.Equal(2, (int)root["schemaVersion"]!);                 // camelCase
        var unit = root["units"]!["anim_idle_1"]!.AsObject();
        Assert.Equal(1, (int)unit["frames"]!);
        Assert.False(unit.ContainsKey("frameWidth"));                // null 欄位省略
        Assert.False(unit.ContainsKey("fps"));
        Assert.False(unit.ContainsKey("loop"));
        Assert.False(unit.ContainsKey("name"));                      // Name 不序列化
    }
}
