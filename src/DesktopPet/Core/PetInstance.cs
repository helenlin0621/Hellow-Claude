using System.Windows.Threading;
using DesktopPet.Core.Visuals;
using DesktopPet.Models;
using DesktopPet.UI;

namespace DesktopPet.Core;

/// <summary>
/// 單一寵物的完整運行單元（設計檔 §3/§3.2/§14）：把 <see cref="MainWindow"/>（視窗 + D3 輸入 +
/// D4 渲染綁定）與 <see cref="Pet"/> 的狀態層（<see cref="StateManager"/> / <see cref="HappinessManager"/> /
/// <see cref="MoodEvaluator"/>）串成一個可獨立啟停的單位，供 E2 的 <c>PetCoordinator</c> 建立 1–2 份。
/// </summary>
/// <remarks>
/// <b>本類別做的事：</b>
/// <list type="number">
///   <item><description>建立 <see cref="MainWindow"/> 並以 <see cref="Pet.SkinFolderPath"/> 呼叫
///     <see cref="MainWindow.LoadSkin"/>（<paramref name="registry"/> 由呼叫端載入一次，雙寵物共用，
///     見 <c>AnimationManager</c> 註解）。</description></item>
///   <item><description>擁有固定 1 Hz 的狀態 tick <see cref="DispatcherTimer"/>（§7.1 步驟 1–3 的骨架，
///     <see cref="StateManager"/>/<see cref="HappinessManager"/> 本身皆為純邏輯、不含計時器，見兩者
///     類別註解「由 D 群/E1 建立」）：每秒呼叫 <see cref="StateManager.Tick"/> +
///     <see cref="HappinessManager.Tick"/>，再以 <see cref="MoodEvaluator"/> 依最新 <c>Hunger</c>/<c>Energy</c>
///     算出心情、寫回 <see cref="Pet.CurrentMood"/>，並驅動 <see cref="MainWindow.SetMood"/>。</description></item>
///   <item><description>訂閱 <see cref="MainWindow.EventTriggered"/> / <see cref="MainWindow.MenuActionRequested"/>，
///     施加設計檔已明確定義、與「哪套素材/哪個視窗」無關的<b>通用互動記帳</b>：任何互動歸零
///     <see cref="Pet.AwakeIdleSeconds"/>（§7.4.2「冷卻期間照常可操作，<c>AwakeIdleSeconds</c> 照歸零」），
///     並依 §7.4.3 對照表呼叫 <see cref="HappinessManager"/> 的對應 <c>TryAward*</c>
///     （<c>CLICK</c>/選單「玩耍」共用 <see cref="HappinessManager.TryAwardClickOrPlay"/>；
///     <c>FEED</c> 對應 <see cref="HappinessManager.TryAwardFeed"/>）。</description></item>
/// </list>
/// <b>本類別刻意不做的事（留給後續任務，避免在整合層塞入未定案的數值規則）：</b>
/// <list type="bullet">
///   <item><description>右鍵選單實際改動 <c>Hunger</c>/<c>Energy</c>（例如餵食扣飢餓、睡眠回能量）、
///     「玩耍」「清潔」的專屬效果、存讀檔與自動保存——設計檔未定義具體數值，且依
///     <c>docs/implementation_plan.md</c> 明確歸屬 E4（「右鍵動作實際改數值」）。</description></item>
///   <item><description>SLEEP 事件的「醒來」判定（§7.3.2「持續至醒來」／§7.4.3「<c>Energy</c> 回滿」）：
///     因 <c>Energy</c> 回充機制尚未定案，本類別只對 <c>SLEEP</c> 觸發做通用互動記帳，
///     不呼叫 <see cref="AnimationManager.EndCurrentEvent"/>；由 E4 依 Energy 回充規則決定何時呼叫
///     （<c>MainWindow</c> 目前未對外公開 <c>AnimationManager</c>，需該任務一併補上出口）。</description></item>
///   <item><description>建立 <see cref="VisualRegistry"/>、決定飼養幾隻、存檔載入——屬 E2/E4。</description></item>
/// </list>
/// <para>
/// 需在 WPF UI 執行緒建立與使用（<see cref="MainWindow"/> 與 <see cref="DispatcherTimer"/> 皆綁定執行緒）。
/// </para>
/// </remarks>
public sealed class PetInstance : IDisposable
{
    private readonly StateManager _stateManager;
    private readonly HappinessManager _happinessManager;
    private readonly MoodEvaluator _moodEvaluator = new();
    private readonly DispatcherTimer _stateTimer;
    private bool _disposed;

    /// <param name="pet">此運行單元對應的寵物資料（就地修改）。</param>
    /// <param name="registry">已載入的視覺類型登記表（§7.3.3；由呼叫端載入一次，多隻寵物可共用同一份）。</param>
    /// <param name="clock">時鐘（預設 <see cref="DateTime.Now"/>）；供 <see cref="StateManager"/> /
    /// <see cref="HappinessManager"/> 共用，避免兩者各自的牆鐘時間漂移，可注入以利測試。</param>
    public PetInstance(Pet pet, VisualRegistry registry, Func<DateTime>? clock = null)
    {
        Pet = pet ?? throw new ArgumentNullException(nameof(pet));
        ArgumentNullException.ThrowIfNull(registry);

        var now = clock ?? (() => DateTime.Now);
        _stateManager = new StateManager(now);
        _happinessManager = new HappinessManager(now);

        Window = new MainWindow();
        Window.EventTriggered += OnEventTriggered;
        Window.MenuActionRequested += OnMenuActionRequested;
        Window.LoadSkin(Pet.SkinFolderPath, registry);
        RefreshMood();

        _stateTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = RenderTickController.StateTickInterval, // 固定 1 Hz（§7.1）。
        };
        _stateTimer.Tick += OnStateTick;
    }

    /// <summary>此運行單元對應的寵物資料。</summary>
    public Pet Pet { get; }

    /// <summary>此運行單元擁有的視窗（供 E2 的 <c>PetCoordinator</c> 定位／顯示多隻寵物時取用）。</summary>
    public MainWindow Window { get; }

    /// <summary>開始運行：顯示視窗並啟動 1 Hz 狀態 tick。</summary>
    public void Start()
    {
        Window.Show();
        _stateTimer.Start();
    }

    /// <summary>1 Hz 狀態 tick（§7.1 步驟 1–3）：推進四項數值與幸福度，重新判定心情並反映到視窗。</summary>
    private void OnStateTick(object? sender, EventArgs e)
    {
        _stateManager.Tick(Pet);
        _happinessManager.Tick(Pet);
        RefreshMood();
    }

    /// <summary>依最新 <c>Hunger</c>/<c>Energy</c> 重新判定心情（§7.2.1），寫回存檔欄位並驅動視窗換圖。</summary>
    private void RefreshMood()
    {
        Pet.CurrentMood = _moodEvaluator.EvaluateMood(Pet);
        Window.SetMood(MoodEvaluator.ToVisualState(Pet.CurrentMood));
    }

    /// <summary>
    /// D3 觸發的視覺事件（僅 Click/Feed/Sleep）：施加通用互動記帳（見類別註解），
    /// 具體扣值效果不在此處理（屬 E4）。
    /// </summary>
    private void OnEventTriggered(object? sender, PetVisualState state)
    {
        Pet.AwakeIdleSeconds = 0; // §7.4.2：任一互動即歸零，不論幸福度回補是否命中冷卻。

        switch (state)
        {
            case PetVisualState.Click:
                _happinessManager.TryAwardClickOrPlay(Pet);
                break;
            case PetVisualState.Feed:
                _happinessManager.TryAwardFeed(Pet);
                break;
            case PetVisualState.Sleep:
                break; // 「睡眠完成」的 +5 需 Energy 回滿判定，尚未定案（見類別註解），此處僅記帳互動。
        }
    }

    /// <summary>右鍵選單中僅「玩耍」與 CLICK 共用幸福度回補規則（§7.4.3）；其餘項目效果未定案，不在此處理。</summary>
    private void OnMenuActionRequested(object? sender, PetMenuAction action)
    {
        if (action != PetMenuAction.Play)
            return;

        Pet.AwakeIdleSeconds = 0;
        _happinessManager.TryAwardClickOrPlay(Pet);
    }

    /// <summary>停止狀態 tick 並解除視窗事件訂閱。不關閉視窗——視窗生命週期交由呼叫端（E2）決定。</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _stateTimer.Stop();
        _stateTimer.Tick -= OnStateTick;
        Window.EventTriggered -= OnEventTriggered;
        Window.MenuActionRequested -= OnMenuActionRequested;
    }
}
