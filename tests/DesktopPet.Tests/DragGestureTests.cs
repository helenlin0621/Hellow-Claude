using DesktopPet.Utils;
using Xunit;

namespace DesktopPet.Tests;

/// <summary>
/// 驗證設計檔 §2.1 拖曳／點擊判定：位移低於閾值視為點擊、達閾值視為拖曳、以歐氏距離（非單軸）
/// 判定、負位移依大小判定、可自訂閾值。閾值本身為 D3 的 UX 決策（設計檔未規定），見
/// <see cref="DragGesture"/> 類別註解。
/// </summary>
public class DragGestureTests
{
    [Fact]
    public void Zero_movement_is_not_a_drag()
    {
        Assert.False(DragGesture.IsDrag(0, 0));
    }

    [Fact]
    public void Movement_below_threshold_is_a_click()
    {
        Assert.False(DragGesture.IsDrag(2, 1)); // sqrt(5) ≈ 2.24 < 4
    }

    [Fact]
    public void Movement_at_threshold_counts_as_drag()
    {
        Assert.True(DragGesture.IsDrag(4, 0));
        Assert.True(DragGesture.IsDrag(0, 4));
    }

    [Fact]
    public void Movement_beyond_threshold_is_a_drag()
    {
        Assert.True(DragGesture.IsDrag(10, 10));
    }

    [Fact]
    public void Diagonal_movement_uses_euclidean_distance_not_axis_values()
    {
        // 3-4-5 三角形：Δx=3, Δy=4，兩軸皆 < 4，但斜線距離恰為 5 ≥ 4，仍應判定為拖曳。
        Assert.True(DragGesture.IsDrag(3, 4));

        // 兩軸皆為 2.9（各自小於閾值 4），但合成距離 sqrt(2*2.9^2) ≈ 4.10 ≥ 4，仍為拖曳。
        Assert.True(DragGesture.IsDrag(2.9, 2.9));
    }

    [Fact]
    public void Negative_deltas_are_judged_by_magnitude()
    {
        Assert.True(DragGesture.IsDrag(-10, -10));
        Assert.False(DragGesture.IsDrag(-1, -1));
    }

    [Fact]
    public void Custom_threshold_is_respected()
    {
        Assert.False(DragGesture.IsDrag(5, 0, threshold: 10));
        Assert.True(DragGesture.IsDrag(15, 0, threshold: 10));
    }
}
