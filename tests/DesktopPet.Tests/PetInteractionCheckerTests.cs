using System;
using System.IO;
using DesktopPet.Core.Interaction;
using DesktopPet.Models;
using Xunit;

namespace DesktopPet.Tests;

/// <summary>
/// 驗證 E3 互動素材檢測（<see cref="PetInteractionChecker"/>，設計檔 §6.5.2 / §6.5.3）：
/// 掃描 <c>interaction_{類型}.png</c>、交集判定、缺資料夾/缺素材不報錯、類型清單去重、
/// 從 <c>interaction_types.json</c> 載入與缺檔 fallback。
/// </summary>
public sealed class PetInteractionCheckerTests : IDisposable
{
    private readonly string _root;

    public PetInteractionCheckerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "pet_interaction_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* 清理失敗不影響測試結果 */ }
    }

    private Pet PetWithAssets(string folderName, params string[] types)
    {
        string dir = Path.Combine(_root, folderName);
        Directory.CreateDirectory(dir);
        foreach (var t in types)
            File.WriteAllText(Path.Combine(dir, $"interaction_{t}.png"), "x"); // 內容不重要，只看檔名存在。

        return new Pet { SkinFolderPath = dir };
    }

    [Fact]
    public void GetInteractionAssetTypes_lists_present_files_in_known_order()
    {
        var checker = new PetInteractionChecker(new[] { "greet", "play", "cuddle" });
        var pet = PetWithAssets("a", "play", "greet"); // 建立順序刻意與登記順序相反。

        var owned = checker.GetInteractionAssetTypes(pet);

        Assert.Equal(new[] { "greet", "play" }, owned); // 維持登記順序，非建立順序。
    }

    [Fact]
    public void GetInteractionAssetTypes_ignores_unknown_types()
    {
        var checker = new PetInteractionChecker(new[] { "greet" });
        var pet = PetWithAssets("a", "greet", "play"); // play 未登記。

        Assert.Equal(new[] { "greet" }, checker.GetInteractionAssetTypes(pet));
    }

    [Fact]
    public void Missing_folder_yields_no_assets_and_no_throw()
    {
        var checker = new PetInteractionChecker(new[] { "greet" });
        var pet = new Pet { SkinFolderPath = Path.Combine(_root, "does_not_exist") };

        Assert.Empty(checker.GetInteractionAssetTypes(pet));
    }

    [Fact]
    public void Intersection_and_CanInteract()
    {
        var checker = new PetInteractionChecker(new[] { "greet", "play", "cuddle" });
        var a = PetWithAssets("a", "greet", "play");
        var b = PetWithAssets("b", "greet");

        Assert.Equal(new[] { "greet" }, checker.GetAvailableInteractionTypes(a, b));
        Assert.True(checker.CanInteract(a, b));
    }

    [Fact]
    public void NoCommonType_cannot_interact()
    {
        var checker = new PetInteractionChecker(new[] { "greet", "play" });
        var a = PetWithAssets("a", "greet");
        var b = PetWithAssets("b", "play");

        Assert.Empty(checker.GetAvailableInteractionTypes(a, b));
        Assert.False(checker.CanInteract(a, b));
    }

    [Fact]
    public void KnownTypes_deduplicates_case_insensitively()
    {
        var checker = new PetInteractionChecker(new[] { "greet", "GREET", " ", "play" });
        Assert.Equal(new[] { "greet", "play" }, checker.KnownTypes);
    }

    [Fact]
    public void ResolveInteractionImagePath_composes_expected_path()
    {
        var pet = new Pet { SkinFolderPath = "/skins/cat" };
        Assert.Equal(
            Path.Combine("/skins/cat", "interaction_greet.png"),
            PetInteractionChecker.ResolveInteractionImagePath(pet, "greet"));
    }

    // ── 從 interaction_types.json 載入 ────────────────────────────
    [Fact]
    public void LoadFromFile_reads_types()
    {
        string json = Path.Combine(_root, "interaction_types.json");
        File.WriteAllText(json, "{ \"types\": [\"greet\", \"cuddle\"] }");

        var checker = PetInteractionChecker.LoadFromFile(json);
        Assert.Equal(new[] { "greet", "cuddle" }, checker.KnownTypes);
    }

    [Fact]
    public void LoadFromFile_missing_falls_back_to_defaults()
    {
        var checker = PetInteractionChecker.LoadFromFile(Path.Combine(_root, "nope.json"));
        Assert.Equal(new[] { "greet", "play", "cuddle" }, checker.KnownTypes);
    }

    [Fact]
    public void LoadFromFile_corrupt_falls_back_to_defaults()
    {
        string json = Path.Combine(_root, "corrupt.json");
        File.WriteAllText(json, "{ not valid json ");

        var checker = PetInteractionChecker.LoadFromFile(json);
        Assert.Equal(new[] { "greet", "play", "cuddle" }, checker.KnownTypes);
    }
}
