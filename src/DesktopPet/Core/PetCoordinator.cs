using System.Linq;
using System.Windows.Threading;
using DesktopPet.Core.Interaction;
using DesktopPet.Core.Visuals;
using DesktopPet.Models;
using DesktopPet.UI;

namespace DesktopPet.Core;

/// <summary>
/// 多寵物協調層（設計檔 §3/§3.1/§6.5）：管理 1–2 個 <see cref="PetInstance"/>（E1）的生命週期，
/// 雙寵物模式下並定時檢查距離與素材以驅動跨寵物互動（§6.5.5「Pet Coordinator 定時檢查距離與素材」）。
/// 飼養數量由 <c>UI/OnboardingWindow</c>（首次啟動）或設置面板（Phase 2）決定，本類別本身不詢問使用者。
/// </summary>
/// <remarks>
/// <b>本類別做的事：</b>
/// <list type="number">
///   <item><description>依傳入的 <see cref="Pet"/> 清單（1–2 隻）各自建立一個 <see cref="PetInstance"/>
///     （共用同一份 <see cref="VisualRegistry"/>，見 <c>AnimationManager</c> 註解：雙寵物的視覺類型
///     定義本就該共用同一份，各自獨立的只有各自的 LRU 快取），並依索引設定
///     <see cref="UI.MainWindow.PlacementIndex"/> 讓多隻寵物的初始視窗不完全疊在一起（§6.1
///     「可分別拖曳到桌面不同位置」——初始仍需彼此看得見，才有得拖）。</description></item>
///   <item><description><b>僅雙寵物模式</b>（<see cref="Count"/> == 2）：以 1 Hz 計時器檢查兩視窗的
///     距離與 <see cref="PetInteractionChecker"/> 的素材交集（§6.5.3），依 §6.5.4 條件表判定
///     <c>greet</c>／<c>cuddle</c> 並在成立時於雙方視窗顯示對應的 <c>interaction_*.png</c>
///     （<see cref="MainWindow.ShowInteraction"/>；條件不成立時 <see cref="MainWindow.ClearInteraction"/>
///     回到正常渲染）；<c>play</c> 則由 <see cref="MainWindow.MenuActionRequested"/> 的「玩耍」項
///     手動觸發（設計檔「使用者手動觸發，或隨機事件」——隨機事件的機率/間隔未定義，見
///     <see cref="InteractionRules"/> 註解，本類別只實作手動觸發那一半）。單寵物模式
///     （<see cref="Count"/> == 1）天然不建立此計時器，即「自動略過互動檢查」。</description></item>
/// </list>
/// <para>
/// <b>本類別刻意不做的事：</b>讀存檔、離線凍結、決定飼養數量的 UI 流程、自動保存——屬 E4；
/// 本類別只接收呼叫端已經準備好的 <see cref="Pet"/> 清單與 <see cref="PetInteractionChecker"/>。
/// </para>
/// </remarks>
public sealed class PetCoordinator : IDisposable
{
    /// <summary>飼養數量上限（§5.1 <c>GameState.MaxPetSlots</c> 固定為 2）。</summary>
    public const int MaxPets = 2;

    /// <summary>手動「玩耍」互動顯示的持續秒數。設計檔未定義互動畫面該顯示多久，僅取與 FEED
    /// （§7.3.2：2.5 秒）同數量級的短暫反饋時長作為預設值；未來設計書若補充可調整。</summary>
    private static readonly TimeSpan PlayInteractionDuration = TimeSpan.FromSeconds(2.5);

    private readonly List<PetInstance> _instances;
    private readonly PetInteractionChecker? _interactionChecker;
    private readonly Func<DateTime> _now;
    private DispatcherTimer? _interactionTimer;
    private int _closeSeconds;
    private DateTime? _playOverrideUntil;
    private bool _disposed;

    /// <param name="pets">要飼養的寵物清單，長度須為 1 或 2（§6.5「使用者可自選飼養 1 隻或 2 隻」）。</param>
    /// <param name="registry">已載入的視覺類型登記表，雙寵物共用同一份（§7.3.3）。</param>
    /// <param name="interactionChecker">互動素材交集判定器（§6.5.3）；<c>null</c> 時使用內建的
    /// 3 種預設類型（<see cref="PetInteractionChecker.DefaultTypes"/>）。單寵物模式下不會用到。</param>
    /// <param name="clock">時鐘，轉交每個 <see cref="PetInstance"/> 並供距離檢查計時器使用
    /// （預設 <see cref="DateTime.Now"/>）。可注入以利測試。</param>
    public PetCoordinator(
        IReadOnlyList<Pet> pets,
        VisualRegistry registry,
        PetInteractionChecker? interactionChecker = null,
        Func<DateTime>? clock = null)
    {
        ArgumentNullException.ThrowIfNull(pets);
        ArgumentNullException.ThrowIfNull(registry);
        if (pets.Count is < 1 or > MaxPets)
            throw new ArgumentOutOfRangeException(nameof(pets), pets.Count, $"飼養數量須為 1 ~ {MaxPets} 隻（§5.1）。");

        _now = clock ?? (() => DateTime.Now);

        _instances = new List<PetInstance>(pets.Count);
        for (int i = 0; i < pets.Count; i++)
        {
            var instance = new PetInstance(pets[i], registry, clock);
            instance.Window.PlacementIndex = i;
            _instances.Add(instance);
        }

        if (_instances.Count == 2)
        {
            _interactionChecker = interactionChecker ?? new PetInteractionChecker();
            _instances[0].Window.MenuActionRequested += OnMenuActionRequested;
            _instances[1].Window.MenuActionRequested += OnMenuActionRequested;
        }
    }

    /// <summary>目前管理的寵物運行單元（唯讀，長度即飼養數量）。</summary>
    public IReadOnlyList<PetInstance> Instances => _instances;

    /// <summary>飼養數量（1 或 2）。單寵物模式（<c>== 1</c>）時互動檢查天然略過，見類別註解。</summary>
    public int Count => _instances.Count;

    /// <summary>啟動所有寵物：依序顯示各自視窗並開始 1 Hz 狀態 tick；雙寵物模式另外啟動互動檢查計時器。</summary>
    public void Start()
    {
        foreach (var instance in _instances)
            instance.Start();

        if (_instances.Count == 2)
        {
            _interactionTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = RenderTickController.StateTickInterval, // 固定 1 Hz，同 §7.1 狀態 tick。
            };
            _interactionTimer.Tick += OnInteractionTick;
            _interactionTimer.Start();
        }
    }

    /// <summary>1 Hz 互動檢查（§6.5.5）：距離 + 素材交集 → greet/cuddle 的自動判定（§6.5.4）。</summary>
    private void OnInteractionTick(object? sender, EventArgs e)
    {
        var a = _instances[0];
        var b = _instances[1];

        // 手動「玩耍」顯示期間不被自動判定覆蓋（進行中不被打斷，呼應 §7.3.2 事件優先權的精神）。
        if (_playOverrideUntil is { } until)
        {
            if (_now() < until)
                return;
            _playOverrideUntil = null;
        }

        double distance = InteractionRules.Distance(
            a.Window.Left + a.Window.Width / 2, a.Window.Top + a.Window.Height / 2,
            b.Window.Left + b.Window.Width / 2, b.Window.Top + b.Window.Height / 2);

        _closeSeconds = InteractionRules.TickCloseSeconds(_closeSeconds, InteractionRules.IsClose(distance));

        var availableTypes = _interactionChecker!.GetAvailableInteractionTypes(a.Pet, b.Pet);

        // cuddle 優先於 greet：長時間依偎是比剛靠近時的招呼更明確的狀態（兩者條件在持續接近下會同時成立）。
        bool cuddle = availableTypes.Contains("cuddle", StringComparer.OrdinalIgnoreCase)
            && InteractionRules.ShouldCuddle(_closeSeconds);
        bool greet = !cuddle
            && availableTypes.Contains("greet", StringComparer.OrdinalIgnoreCase)
            && InteractionRules.ShouldGreet(distance, !a.Window.IsEventActive, !b.Window.IsEventActive);

        if (cuddle)
            ShowInteractionOnBoth(a, b, "cuddle");
        else if (greet)
            ShowInteractionOnBoth(a, b, "greet");
        else
            ClearInteractionOnBoth(a, b);
    }

    /// <summary>
    /// 「玩耍」選單項的雙寵物互動一半（§6.5.4：手動觸發無距離門檻，只看素材交集）。單寵物模式
    /// 不訂閱此事件；已有 <see cref="PetInstance"/> 對單寵物「玩耍」幸福度回補的獨立處理，不受影響。
    /// </summary>
    private void OnMenuActionRequested(object? sender, PetMenuAction action)
    {
        if (action != PetMenuAction.Play || _interactionChecker is null)
            return;

        var a = _instances[0];
        var b = _instances[1];
        var availableTypes = _interactionChecker.GetAvailableInteractionTypes(a.Pet, b.Pet);
        if (!availableTypes.Contains("play", StringComparer.OrdinalIgnoreCase))
            return; // 無交集 → 各自獨立行動，不強迫顯示（§6.5.3）。

        _playOverrideUntil = _now() + PlayInteractionDuration;
        ShowInteractionOnBoth(a, b, "play");
    }

    private static void ShowInteractionOnBoth(PetInstance a, PetInstance b, string type)
    {
        if (PetInteractionChecker.ResolveInteractionImagePath(a.Pet.SkinFolderPath, type) is { } pathA)
            a.Window.ShowInteraction(pathA);
        if (PetInteractionChecker.ResolveInteractionImagePath(b.Pet.SkinFolderPath, type) is { } pathB)
            b.Window.ShowInteraction(pathB);
    }

    private static void ClearInteractionOnBoth(PetInstance a, PetInstance b)
    {
        a.Window.ClearInteraction();
        b.Window.ClearInteraction();
    }

    /// <summary>停止所有寵物的狀態 tick、互動檢查計時器，並解除事件訂閱（見 <see cref="PetInstance.Dispose"/>）。</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _interactionTimer?.Stop();
        if (_interactionTimer is not null)
            _interactionTimer.Tick -= OnInteractionTick;

        if (_instances.Count == 2)
        {
            _instances[0].Window.MenuActionRequested -= OnMenuActionRequested;
            _instances[1].Window.MenuActionRequested -= OnMenuActionRequested;
        }

        foreach (var instance in _instances)
            instance.Dispose();
    }
}
