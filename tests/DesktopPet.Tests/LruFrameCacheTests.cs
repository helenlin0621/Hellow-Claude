using System;
using DesktopPet.Core.Skins;
using Xunit;

namespace DesktopPet.Tests;

/// <summary>
/// 驗證設計檔 §7.3.6 資源載入策略：延遲載入（首次抽中才解碼、之後命中快取）、
/// LRU 以<b>格數</b>計容量（預設 48）、最近使用者受保護、超容量單元仍可單獨載入。
/// 以 <see cref="LruFrameCache{TValue}"/> 的泛型（此處 <c>string</c>）與 <c>DecodeCount</c> 觀測，
/// 不依賴 WPF <c>BitmapSource</c>，可跨平台執行。
/// </summary>
public class LruFrameCacheTests
{
    private static string Decode(string key) => "decoded:" + key;

    [Fact]
    public void Default_capacity_is_48_frames()
    {
        Assert.Equal(48, LruFrameCache<string>.DefaultFrameCapacity);
        Assert.Equal(48, new LruFrameCache<string>().FrameCapacity);
    }

    [Fact]
    public void Constructor_rejects_capacity_below_one()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LruFrameCache<string>(0));
    }

    // ── 延遲載入：僅首次抽中才解碼 ─────────────────────────────
    [Fact]
    public void Decodes_only_on_first_miss_then_serves_from_cache()
    {
        var cache = new LruFrameCache<string>(48);

        var a1 = cache.Get("a", 1, () => Decode("a"));
        var a2 = cache.Get("a", 1, () => Decode("a"));   // 命中，不再解碼

        Assert.Same(a1, a2);
        Assert.Equal(1, cache.DecodeCount);
        Assert.Equal(1, cache.Count);

        cache.Get("b", 1, () => Decode("b"));            // 新鍵 → 再解碼一次
        Assert.Equal(2, cache.DecodeCount);
    }

    [Fact]
    public void Tracks_current_frames_and_count()
    {
        var cache = new LruFrameCache<string>(48);
        cache.Get("a", 8, () => Decode("a"));
        cache.Get("b", 4, () => Decode("b"));

        Assert.Equal(12, cache.CurrentFrames);
        Assert.Equal(2, cache.Count);
    }

    // ── LRU 淘汰：以格數超出容量時逐出最久未使用者 ──────────────
    [Fact]
    public void Evicts_least_recently_used_when_frame_budget_exceeded()
    {
        var cache = new LruFrameCache<string>(10);
        cache.Get("a", 4, () => Decode("a"));
        cache.Get("b", 4, () => Decode("b"));
        cache.Get("c", 4, () => Decode("c"));   // 8+4=12 > 10 → 逐出 a

        Assert.False(cache.Contains("a"));
        Assert.True(cache.Contains("b"));
        Assert.True(cache.Contains("c"));
        Assert.Equal(8, cache.CurrentFrames);
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void Accessing_an_entry_marks_it_most_recently_used()
    {
        var cache = new LruFrameCache<string>(10);
        cache.Get("a", 4, () => Decode("a"));
        cache.Get("b", 4, () => Decode("b"));
        cache.Get("a", 4, () => Decode("a"));   // 命中 → a 變 MRU、b 變 LRU
        cache.Get("c", 4, () => Decode("c"));   // 需逐出 → 逐出 b（非 a）

        Assert.True(cache.Contains("a"));
        Assert.False(cache.Contains("b"));
        Assert.True(cache.Contains("c"));
    }

    // ── 單一單元格數超過容量：仍載入（清空其餘）────────────────
    [Fact]
    public void Single_unit_larger_than_capacity_is_still_cached_alone()
    {
        var cache = new LruFrameCache<string>(10);
        cache.Get("a", 4, () => Decode("a"));
        cache.Get("b", 4, () => Decode("b"));

        var big = cache.Get("big", 16, () => Decode("big"));   // 逐出全部後單獨放入

        Assert.Equal("decoded:big", big);
        Assert.True(cache.Contains("big"));
        Assert.False(cache.Contains("a"));
        Assert.False(cache.Contains("b"));
        Assert.Equal(1, cache.Count);
        Assert.Equal(16, cache.CurrentFrames);
    }

    // ── 韌性 / 工具方法 ────────────────────────────────────────
    [Fact]
    public void Frames_below_one_are_normalized_to_one()
    {
        var cache = new LruFrameCache<string>(48);
        cache.Get("x", 0, () => Decode("x"));
        Assert.Equal(1, cache.CurrentFrames);
    }

    [Fact]
    public void Clear_empties_the_cache()
    {
        var cache = new LruFrameCache<string>(48);
        cache.Get("a", 8, () => Decode("a"));
        cache.Clear();

        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.CurrentFrames);
        Assert.False(cache.Contains("a"));
    }

    [Fact]
    public void Get_null_arguments_throw()
    {
        var cache = new LruFrameCache<string>(48);
        Assert.Throws<ArgumentNullException>(() => cache.Get(null!, 1, () => "v"));
        Assert.Throws<ArgumentNullException>(() => cache.Get("k", 1, null!));
    }
}
