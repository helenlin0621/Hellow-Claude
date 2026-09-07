using System.Windows.Threading;
using DesktopPet.Core.Interaction;
using DesktopPet.Core.Visuals;
using DesktopPet.Models;

namespace DesktopPet.Core;

/// <summary>
/// 多寵物協調層（E2/E3，設計檔 §3.1 / §6.5）：管理 1–2 隻 <see cref="PetInstance"/> 的生命週期，
/// 並在雙寵物模式下定時檢查距離與素材以觸發跨寵物互動（§6.5.3 / §6.5.4）。
/// <b>單寵物模式時不建立第 2 隻，互動檢查自動略過</b>（§3.1）。
/// </summary>
/// <remarks>
/// <b>互動＝漸進式增強（§6.5）：</b>只有在「養 2 隻」且雙方有共同互動素材類型（交集非空，§6.5.3）時，
/// 才啟動互動 tick；缺素材或單寵物時各自獨立行動，不報錯、不卡住。
/// <para>
/// <b>互動觸發與防刷：</b>互動 tick（<see cref="InteractionTickInterval"/>）依 <see cref="InteractionRules"/>
/// 判定此刻該觸發哪一種互動（§6.5.4）。實際「發放幸福度 + 播互動素材」以 §7.4.3 的 30 分鐘互動冷卻
/// 為閘門（<see cref="PetInstance.TryAwardInteraction"/>）：冷卻中就不觸發顯示，避免兩隻靠在一起時每隔幾秒
/// 狂刷互動動畫。互動冷卻與點擊冷卻共用 <see cref="Pet.LastInteractionTime"/>（§7.4.6 的刻意耦合）。
/// </para>
/// </remarks>
public sealed class PetCoordinator : IDisposable
{
    /// <summary>互動檢查頻率（§6.5.5：定時檢查距離與素材；此為兼顧反應與負擔的取捨值）。</summary>
    public static readonly TimeSpan InteractionTickInterval = TimeSpan.FromSeconds(2);

    private readonly GameState _state;
    private readonly PetInteractionChecker _checker;
    private readonly List<PetInstance> _instances = new();

    private DispatcherTimer? _interactionTimer;   // 僅雙寵物且有互動素材時建立。
    private DispatcherTimer? _interactionEndTimer; // 互動素材顯示的一次性結束計時。

    private int _sustainedCloseSeconds;   // 連續維持接近的秒數（§6.5.4 cuddle 判定）。
    private bool _interactionDisplaying;  // 互動素材顯示中：期間不重複觸發。

    /// <param name="state">執行期狀態（含 1–2 隻寵物與設定）。就地承載，供自動保存讀取（§8.2）。</param>
    /// <param name="registry">已載入的視覺類型登記表（§7.3.3；雙寵物共用同一份）。</param>
    /// <param name="checker">互動素材檢測器（§6.5.3；由 <c>interaction_types.json</c> 載入）。</param>
    /// <param name="clock">時鐘（預設 <see cref="DateTime.Now"/>）。傳給各 <see cref="PetInstance"/>，可注入以利測試。</param>
    public PetCoordinator(GameState state, VisualRegistry registry, PetInteractionChecker checker, Func<DateTime>? clock = null)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        ArgumentNullException.ThrowIfNull(registry);
        _checker = checker ?? throw new ArgumentNullException(nameof(checker));

        foreach (var pet in _state.Pets)
        {
            var instance = new PetInstance(pet, registry, clock);
            ApplySettings(instance.Window, _state.Settings);
            _instances.Add(instance);
        }
    }

    /// <summary>目前的寵物實例（1–2 隻，唯讀）。</summary>
    public IReadOnlyList<PetInstance> Instances => _instances;

    /// <summary>顯示所有寵物視窗、啟動各自的 1 Hz 狀態 tick，並在符合條件時啟動互動檢查（§6.5）。</summary>
    public void Start()
    {
        foreach (var instance in _instances)
            instance.Start();

        StartInteractionIfEligible();
    }

    /// <summary>把全域設定套用到單一寵物視窗（§6.1 置頂、§2.1 點穿）。</summary>
    private static void ApplySettings(UI.MainWindow window, Settings settings)
    {
        window.Topmost = settings.AlwaysOnTop;
        window.ClickThrough = settings.ClickThrough;
    }

    /// <summary>
    /// 僅在「養 2 隻」且雙方有共同互動素材（§6.5.3 交集非空）時，啟動互動 tick；
    /// 否則各自獨立行動，不建立計時器（§3.1 / §6.5）。
    /// </summary>
    private void StartInteractionIfEligible()
    {
        if (_instances.Count < 2)
            return; // 單寵物：略過互動檢查（§3.1）。

        if (!_checker.CanInteract(_instances[0].Pet, _instances[1].Pet))
            return; // 無共同互動素材：各自獨立（§6.5.3）。

        _interactionTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = InteractionTickInterval,
        };
        _interactionTimer.Tick += OnInteractionTick;
        _interactionTimer.Start();
    }

    /// <summary>
    /// 一次互動檢查（§6.5.4）：更新「維持接近」計時 → 依 <see cref="InteractionRules"/> 挑類型 →
    /// 通過 30 分互動冷卻即對兩隻各發 +3 並播互動素材（§7.4.3）。互動素材顯示期間不重複觸發。
    /// </summary>
    private void OnInteractionTick(object? sender, EventArgs e)
    {
        if (_interactionDisplaying)
            return;

        var a = _instances[0];
        var b = _instances[1];

        double distance = InteractionRules.Distance(
            a.Window.CenterX, a.Window.CenterY,
            b.Window.CenterX, b.Window.CenterY);

        // 維持接近的連續秒數（§6.5.4 cuddle）；一離開接近距離即歸零。
        if (InteractionRules.IsClose(distance))
            _sustainedCloseSeconds += (int)InteractionTickInterval.TotalSeconds;
        else
            _sustainedCloseSeconds = 0;

        bool bothIdle = !a.Window.HasActiveEvent && !b.Window.HasActiveEvent;
        var available = _checker.GetAvailableInteractionTypes(a.Pet, b.Pet);

        string? type = InteractionRules.ChooseInteractionType(available, distance, _sustainedCloseSeconds, bothIdle);
        if (type is null)
            return;

        // 以互動冷卻為閘門（§7.4.3）：兩隻皆通過冷卻才發放並顯示，避免靠在一起時狂刷。
        bool awardedA = a.TryAwardInteraction();
        bool awardedB = b.TryAwardInteraction();
        if (!awardedA || !awardedB)
            return;

        PlayInteraction(a, b, type);
    }

    /// <summary>對兩隻各顯示對應的 <c>interaction_{類型}.png</c>（§6.5.2），並排程結束顯示。</summary>
    private void PlayInteraction(PetInstance a, PetInstance b, string type)
    {
        a.Window.PlayInteractionImage(PetInteractionChecker.ResolveInteractionImagePath(a.Pet, type));
        b.Window.PlayInteractionImage(PetInteractionChecker.ResolveInteractionImagePath(b.Pet, type));

        _interactionDisplaying = true;
        _interactionEndTimer ??= new DispatcherTimer(DispatcherPriority.Background);
        _interactionEndTimer.Interval = InteractionRules.InteractionDisplayDuration;
        _interactionEndTimer.Tick -= OnInteractionEnd; // 避免重複訂閱。
        _interactionEndTimer.Tick += OnInteractionEnd;
        _interactionEndTimer.Start();
    }

    private void OnInteractionEnd(object? sender, EventArgs e)
    {
        _interactionEndTimer?.Stop();
        _interactionDisplaying = false;

        foreach (var instance in _instances)
            instance.Window.EndInteraction();
    }

    /// <summary>停止互動計時並釋放所有寵物實例（關閉時）。</summary>
    public void Dispose()
    {
        if (_interactionTimer is not null)
        {
            _interactionTimer.Stop();
            _interactionTimer.Tick -= OnInteractionTick;
            _interactionTimer = null;
        }

        if (_interactionEndTimer is not null)
        {
            _interactionEndTimer.Stop();
            _interactionEndTimer.Tick -= OnInteractionEnd;
            _interactionEndTimer = null;
        }

        foreach (var instance in _instances)
            instance.Dispose();

        _instances.Clear();
    }
}
