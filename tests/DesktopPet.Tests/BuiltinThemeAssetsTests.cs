using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DesktopPet.Core.Skins;
using DesktopPet.Core.Visuals;
using Xunit;

namespace DesktopPet.Tests;

/// <summary>
/// 驗證 B7 隨程式發佈的資料檔與內建主題彼此一致（§6.4.1/§7.3.3/§11.1）：
/// <c>pet_visuals.json</c> 解析為標準 6 類型、每套主題的 <c>skin.json</c> 與實際 PNG 尺寸相符
/// （<c>frames × frameWidth == 圖寬</c>）、NEUTRAL 必有素材、互動素材齊備。純檔案 IO，跨平台可跑。
/// </summary>
public class BuiltinThemeAssetsTests
{
    private const int FrameSize = 256; // §11.1 建議 256×256

    private static string ResourcesDir([CallerFilePath] string thisFile = "")
    {
        // 由本測試檔位置（tests/DesktopPet.Tests/）回到 repo 根，再進 src/DesktopPet/Resources。
        var repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));
        return Path.Combine(repoRoot, "src", "DesktopPet", "Resources");
    }

    private static (int width, int height) PngSize(string path)
    {
        using var fs = File.OpenRead(path);
        Span<byte> head = stackalloc byte[26];
        int read = fs.Read(head);
        Assert.True(read >= 26, $"PNG 太短：{path}");
        // 8 位元組簽章 + IHDR(長度4+tag4) 後即為 width(4)/height(4)（大端）。
        int width = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
        int height = (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
        return (width, height);
    }

    [Fact]
    public void Shipped_pet_visuals_parses_to_the_six_canonical_types()
    {
        var reg = VisualRegistry.LoadFromFile(Path.Combine(ResourcesDir(), "pet_visuals.json"));

        Assert.Equal(6, reg.Definitions.Count);
        Assert.True(reg.GetDefinition(PetVisualState.Neutral)!.Required);
        Assert.Equal("anim_tired", reg.GetDefinition(PetVisualState.LowEnergy)!.Prefix);
        Assert.Equal(PetVisualState.Neutral, reg.GetDefinition(PetVisualState.Sad)!.Fallback);
        Assert.Equal(PetVisualState.LowEnergy, reg.GetDefinition(PetVisualState.Sleep)!.Fallback);
        Assert.Equal(1.5, reg.GetDefinition(PetVisualState.Click)!.DurationSec);
    }

    [Fact]
    public void Shipped_interaction_types_lists_the_three_types()
    {
        var json = File.ReadAllText(Path.Combine(ResourcesDir(), "interaction_types.json"));
        var doc = JsonSerializer.Deserialize<InteractionTypesFile>(
            json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.Equal(new[] { "greet", "play", "cuddle" }, doc.Types);
    }

    [Theory]
    [InlineData("builtin_cat")]
    [InlineData("builtin_dog")]
    public void Builtin_theme_skin_json_matches_actual_png_dimensions(string theme)
    {
        var themeDir = Path.Combine(ResourcesDir(), "Assets", "Themes", theme);
        Assert.True(Directory.Exists(themeDir), $"缺內建主題資料夾：{themeDir}");

        var manifest = SkinManifest.Load(themeDir);
        var registry = VisualRegistry.LoadFromFile(Path.Combine(ResourcesDir(), "pet_visuals.json"));
        var pool = registry.ScanUnits(themeDir);

        // NEUTRAL 必有素材（§7.3.4），且本佔位集含 3 個 idle 單元。
        Assert.True(pool.ContainsKey(PetVisualState.Neutral));
        Assert.Equal(3, pool[PetVisualState.Neutral].Count);

        // 每個掃描到的 anim 單元：PNG 存在，且 frames × frameWidth == 圖寬、圖高 == 256。
        foreach (var units in pool.Values)
        {
            foreach (var unitName in units)
            {
                var png = Path.Combine(themeDir, unitName + ".png");
                Assert.True(File.Exists(png), $"缺 PNG：{png}");

                var info = manifest.GetUnit(unitName);
                int frameWidth = info.FrameWidth ?? FrameSize;
                var (w, h) = PngSize(png);
                Assert.Equal(frameWidth * info.Frames, w);
                Assert.Equal(FrameSize, h);
            }
        }

        // 互動素材齊備（§6.5.2；固定單張靜態）。
        foreach (var type in new[] { "greet", "play", "cuddle" })
            Assert.True(File.Exists(Path.Combine(themeDir, $"interaction_{type}.png")));
    }

    private sealed class InteractionTypesFile
    {
        public List<string> Types { get; set; } = new();
    }
}
