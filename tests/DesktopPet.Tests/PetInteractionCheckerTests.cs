using System.IO;
using DesktopPet.Core.Interaction;
using DesktopPet.Models;
using Xunit;

namespace DesktopPet.Tests;

/// <summary>
/// 驗證設計檔 §6.5.2/§6.5.3：互動素材的交集判定（有交集才能互動，無交集各自獨立不報錯）、
/// 檔名規則 <c>interaction_[類型].png</c>、<c>interaction_types.json</c> 缺檔／破損時退回
/// 3 種預設類型。皆為純邏輯（檔案存在性檢查），跨平台可跑。
/// </summary>
public class PetInteractionCheckerTests : IDisposable
{
    private readonly string _dirA;
    private readonly string _dirB;

    public PetInteractionCheckerTests()
    {
        _dirA = CreateTempDir("A");
        _dirB = CreateTempDir("B");
    }

    public void Dispose()
    {
        TryDelete(_dirA);
        TryDelete(_dirB);
    }

    private static string CreateTempDir(string suffix)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"DesktopPetInteractionTests_{suffix}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void TryDelete(string dir)
    {
        try { Directory.Delete(dir, recursive: true); } catch { /* 忽略清理錯誤 */ }
    }

    private static void Touch(string dir, string fileName) => File.WriteAllText(Path.Combine(dir, fileName), "x");

    private static Pet PetWith(string skinFolderPath) => new() { SkinFolderPath = skinFolderPath };

    // ── 交集判定（§6.5.3）───────────────────────────────────────────
    [Fact]
    public void Intersection_returns_only_shared_types()
    {
        Touch(_dirA, "interaction_greet.png");
        Touch(_dirA, "interaction_play.png");
        Touch(_dirB, "interaction_greet.png"); // B 只有 greet

        var checker = new PetInteractionChecker();
        var types = checker.GetAvailableInteractionTypes(PetWith(_dirA), PetWith(_dirB));

        Assert.Equal(new[] { "greet" }, types);
    }

    [Fact]
    public void No_shared_types_returns_empty_and_cannot_interact()
    {
        Touch(_dirA, "interaction_greet.png");
        Touch(_dirB, "interaction_cuddle.png"); // 完全不重疊

        var checker = new PetInteractionChecker();
        Assert.Empty(checker.GetAvailableInteractionTypes(PetWith(_dirA), PetWith(_dirB)));
        Assert.False(checker.CanInteract(PetWith(_dirA), PetWith(_dirB)));
    }

    [Fact]
    public void All_three_types_shared_returns_all_three()
    {
        foreach (var type in new[] { "greet", "play", "cuddle" })
        {
            Touch(_dirA, $"interaction_{type}.png");
            Touch(_dirB, $"interaction_{type}.png");
        }

        var checker = new PetInteractionChecker();
        var types = checker.GetAvailableInteractionTypes(PetWith(_dirA), PetWith(_dirB));

        Assert.Equal(3, types.Count);
        Assert.True(checker.CanInteract(PetWith(_dirA), PetWith(_dirB)));
    }

    [Fact]
    public void Missing_skin_folder_yields_no_types_without_throwing()
    {
        var checker = new PetInteractionChecker();
        var types = checker.GetAvailableInteractionTypes(
            PetWith(Path.Combine(_dirA, "does_not_exist")), PetWith(_dirB));

        Assert.Empty(types);
    }

    // ── 檔名規則：延續靜態圖片規格（§6.5.2）──────────────────────────
    [Fact]
    public void Accepts_jpg_and_is_case_insensitive_on_extension()
    {
        Touch(_dirA, "interaction_greet.JPG");
        Touch(_dirB, "interaction_greet.jpg");

        var checker = new PetInteractionChecker();
        Assert.Equal(new[] { "greet" }, checker.GetAvailableInteractionTypes(PetWith(_dirA), PetWith(_dirB)));
    }

    [Fact]
    public void ResolveInteractionImagePath_returns_null_when_missing()
    {
        Assert.Null(PetInteractionChecker.ResolveInteractionImagePath(_dirA, "greet"));

        Touch(_dirA, "interaction_greet.png");
        Assert.NotNull(PetInteractionChecker.ResolveInteractionImagePath(_dirA, "greet"));
    }

    // ── interaction_types.json：載入 / 缺檔 / 破損（§6.5.2）─────────────
    [Fact]
    public void LoadFromFile_missing_file_falls_back_to_default_types()
    {
        var checker = PetInteractionChecker.LoadFromFile(Path.Combine(_dirA, "does_not_exist.json"));

        Touch(_dirA, "interaction_future_type.png");
        Touch(_dirB, "interaction_future_type.png");
        // 預設類型清單只有 greet/play/cuddle，不含 future_type，故不會被判定為交集。
        Assert.Empty(checker.GetAvailableInteractionTypes(PetWith(_dirA), PetWith(_dirB)));
    }

    [Fact]
    public void LoadFromFile_parses_custom_type_list()
    {
        var jsonPath = Path.Combine(_dirA, "interaction_types.json");
        File.WriteAllText(jsonPath, """{ "types": ["greet", "future_type"] }""");

        Touch(_dirA, "interaction_future_type.png");
        Touch(_dirB, "interaction_future_type.png");

        var checker = PetInteractionChecker.LoadFromFile(jsonPath);
        Assert.Equal(new[] { "future_type" }, checker.GetAvailableInteractionTypes(PetWith(_dirA), PetWith(_dirB)));
    }

    [Fact]
    public void LoadFromFile_corrupt_json_falls_back_to_default_types()
    {
        var jsonPath = Path.Combine(_dirA, "interaction_types.json");
        File.WriteAllText(jsonPath, "{ not valid ");

        Touch(_dirA, "interaction_greet.png");
        Touch(_dirB, "interaction_greet.png");

        var checker = PetInteractionChecker.LoadFromFile(jsonPath);
        Assert.Equal(new[] { "greet" }, checker.GetAvailableInteractionTypes(PetWith(_dirA), PetWith(_dirB)));
    }
}
