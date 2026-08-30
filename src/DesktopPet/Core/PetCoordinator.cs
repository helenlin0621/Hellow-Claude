using DesktopPet.Core.Visuals;
using DesktopPet.Models;

namespace DesktopPet.Core;

/// <summary>
/// 多寵物協調層（設計檔 §3/§3.1/§6.5）：管理 1–2 個 <see cref="PetInstance"/>（E1）的生命週期。
/// 飼養數量由 <c>UI/OnboardingWindow</c>（首次啟動）或設置面板（Phase 2）決定，本類別本身不詢問使用者。
/// </summary>
/// <remarks>
/// <b>本類別做的事：</b>依傳入的 <see cref="Pet"/> 清單（1–2 隻）各自建立一個 <see cref="PetInstance"/>
/// （共用同一份 <see cref="VisualRegistry"/>，見 <c>AnimationManager</c> 註解：雙寵物的視覺類型定義本就
/// 該共用同一份，各自獨立的只有各自的 LRU 快取），並依索引設定
/// <see cref="UI.MainWindow.PlacementIndex"/> 讓多隻寵物的初始視窗不完全疊在一起（§6.1「可分別拖曳到
/// 桌面不同位置」——初始仍需彼此看得見，才有得拖）。
/// <para>
/// <b>本類別刻意不做的事：</b>
/// <list type="bullet">
///   <item><description><b>跨寵物互動判定</b>（§6.5.2–§6.5.4：<c>PetInteractionChecker</c> 交集判定、
///     距離、<c>interaction_*.png</c> 播放）——屬 E3。單寵物模式因 <see cref="Instances"/> 只有一個
///     元素，天然不會跑到任何互動邏輯；<see cref="Count"/> == 1 這件事本身就是「自動略過互動檢查」，
///     E3 加入互動檢查時只需在 <see cref="Count"/> == 2 時才執行。</description></item>
///   <item><description>讀存檔、離線凍結、決定飼養數量的 UI 流程、自動保存——屬 E4；本類別只接收
///     呼叫端已經準備好的 <see cref="Pet"/> 清單。</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class PetCoordinator : IDisposable
{
    /// <summary>飼養數量上限（§5.1 <c>GameState.MaxPetSlots</c> 固定為 2）。</summary>
    public const int MaxPets = 2;

    private readonly List<PetInstance> _instances;
    private bool _disposed;

    /// <param name="pets">要飼養的寵物清單，長度須為 1 或 2（§6.5「使用者可自選飼養 1 隻或 2 隻」）。</param>
    /// <param name="registry">已載入的視覺類型登記表，雙寵物共用同一份（§7.3.3）。</param>
    /// <param name="clock">時鐘，轉交每個 <see cref="PetInstance"/>（預設 <see cref="DateTime.Now"/>）。可注入以利測試。</param>
    public PetCoordinator(IReadOnlyList<Pet> pets, VisualRegistry registry, Func<DateTime>? clock = null)
    {
        ArgumentNullException.ThrowIfNull(pets);
        ArgumentNullException.ThrowIfNull(registry);
        if (pets.Count is < 1 or > MaxPets)
            throw new ArgumentOutOfRangeException(nameof(pets), pets.Count, $"飼養數量須為 1 ~ {MaxPets} 隻（§5.1）。");

        _instances = new List<PetInstance>(pets.Count);
        for (int i = 0; i < pets.Count; i++)
        {
            var instance = new PetInstance(pets[i], registry, clock);
            instance.Window.PlacementIndex = i;
            _instances.Add(instance);
        }
    }

    /// <summary>目前管理的寵物運行單元（唯讀，長度即飼養數量）。</summary>
    public IReadOnlyList<PetInstance> Instances => _instances;

    /// <summary>飼養數量（1 或 2）。單寵物模式（<c>== 1</c>）時互動檢查天然略過，見類別註解。</summary>
    public int Count => _instances.Count;

    /// <summary>啟動所有寵物：依序顯示各自視窗並開始 1 Hz 狀態 tick。</summary>
    public void Start()
    {
        foreach (var instance in _instances)
            instance.Start();
    }

    /// <summary>停止所有寵物的狀態 tick 並解除事件訂閱（見 <see cref="PetInstance.Dispose"/>）。</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        foreach (var instance in _instances)
            instance.Dispose();
    }
}
