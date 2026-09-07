using System.Windows.Threading;
using DesktopPet.Core.Visuals;
using DesktopPet.Models;
using DesktopPet.UI;

namespace DesktopPet.Core;

/// <summary>
/// 單一寵物的完整運行單元（E1，設計檔 §3 / §3.2）：把「視窗（<see cref="MainWindow"/>）＋狀態
/// （<see cref="StateManager"/> / <see cref="HappinessManager"/>）＋視覺（<c>AnimationManager</c>，
/// 由視窗持有）＋輸入（視窗事件）」串成一個獨立運行的寵物實例。每隻寵物一份，互不共用狀態
/// （§6.5.5：獨立視窗、獨立狀態、獨立圖樣）。
/// </summary>
/// <remarks>
/// <b>職責：</b>擁有並驅動該寵物的 <b>1 Hz 狀態 tick</b>（<see cref="DispatcherTimer"/>）：每秒推進
/// <see cref="StateManager"/> 與 <see cref="HappinessManager"/>，重算心情（<see cref="MoodEvaluator"/>）
/// 並更新視窗；處理視窗拋出的輸入事件（點擊/餵食/睡眠/選單）對應的數值效果（§7.4.3 幸福度回補、
/// §6.3 照顧動作，見 <see cref="PetCareActions"/>）。
/// <para>
/// <b>核心不變量遵循：</b>
/// <list type="bullet">
///   <item><description>任何互動（點擊/餵食/睡眠/玩耍/清潔/雙寵物互動）都把 <see cref="Pet.AwakeIdleSeconds"/>
///     歸零（§7.4.2），冷卻只 gate 幸福度加成、不 gate 操作本身（§7.4.3）。</description></item>
///   <item><description>心情只由 <c>Hunger</c>/<c>Energy</c> 決定（§7.2.1），與 <c>Happiness</c> 無關。</description></item>
///   <item><description>離線凍結由上層（App/E4）在載入後、<see cref="Start"/> 前套用（§7.4.4）；本類別不補算離線。</description></item>
/// </list>
/// </para>
/// 渲染綁定（把 <c>FrameRef</c> 畫到 <c>Image</c>、動態渲染頻率）已由 D4 的 <see cref="MainWindow"/> 內部處理，
/// 本類別只呼叫 <see cref="MainWindow.SetMood"/> / <see cref="MainWindow.EndCurrentAnimationEvent"/> 等公開面。
/// </remarks>
public sealed class PetInstance : IDisposable
{
    /// <summary>狀態 tick 頻率（§7.1：固定 1 Hz）。</summary>
    public static readonly TimeSpan StateTickInterval = TimeSpan.FromSeconds(1);

    private readonly Pet _pet;
    private readonly MainWindow _window;
    private readonly StateManager _state;
    private readonly HappinessManager _happiness;
    private readonly MoodEvaluator _mood = new();
    private readonly DispatcherTimer _stateTimer;

    // 睡眠事件進行中：每個 1 Hz tick 回補能量，回滿即醒來（§7.3.2）。
    private bool _sleeping;

    /// <param name="pet">此實例承載的寵物資料（就地修改；來自存檔或 <see cref="PetFactory"/>）。</param>
    /// <param name="registry">已載入的視覺類型登記表（§7.3.3；雙寵物共用同一份）。</param>
    /// <param name="clock">時鐘（預設 <see cref="DateTime.Now"/>）。狀態/幸福度共用同一時鐘，可注入以利測試。</param>
    public PetInstance(Pet pet, VisualRegistry registry, Func<DateTime>? clock = null)
    {
        _pet = pet ?? throw new ArgumentNullException(nameof(pet));
        ArgumentNullException.ThrowIfNull(registry);

        var now = clock ?? (() => DateTime.Now);
        _state = new StateManager(now);
        _happiness = new HappinessManager(now);

        _window = new MainWindow();
        _window.LoadSkin(_pet.SkinFolderPath, registry);
        _window.SetMood(_mood.EvaluateVisualState(_pet)); // 依載入時的數值決定初始心情。

        _window.EventTriggered += OnEventTriggered;
        _window.MenuActionRequested += OnMenuActionRequested;

        _stateTimer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = StateTickInterval,
        };
        _stateTimer.Tick += OnStateTick;
    }

    /// <summary>此實例承載的寵物（供 E2 <c>PetCoordinator</c> 做互動判定與存檔）。</summary>
    public Pet Pet => _pet;

    /// <summary>此實例的視窗（供 E2 讀取中心座標 / 閒置狀態、顯示互動素材）。</summary>
    public MainWindow Window => _window;

    /// <summary>顯示視窗並啟動 1 Hz 狀態 tick。應在離線凍結套用後呼叫（§7.1 啟動步驟 0→1）。</summary>
    public void Start()
    {
        _window.Show();
        _stateTimer.Start();
    }

    /// <summary>
    /// 一次 1 Hz 狀態 tick（§7.1）：推進數值 → 睡眠回補 → 重算心情並更新視窗。
    /// </summary>
    private void OnStateTick(object? sender, EventArgs e)
    {
        StateTickResult stateResult = _state.Tick(_pet);
        _happiness.Tick(_pet); // 幸福度衰減（每小時結算，多數 tick 為 no-op）。

        bool moodMayChange = stateResult.AnyValueChanged;

        if (_sleeping)
        {
            // 睡眠期間回補能量（凌駕自然衰減）；回滿即醒來並發放「睡眠完成 +5」（§7.4.3）。
            if (PetCareActions.RecoverEnergyForSleep(_pet))
                WakeFromSleep();

            moodMayChange = true; // 能量變動可能改變心情（LOW_ENERGY → NEUTRAL）。
        }

        if (moodMayChange)
            _window.SetMood(_mood.EvaluateVisualState(_pet));
    }

    /// <summary>
    /// 視覺事件（§7.3.2）的數值效果：<see cref="MainWindow"/> 已負責觸發動畫，本處只施加對應的
    /// 數值變化與幸福度回補，並歸零冷落計時（§7.4.2）。
    /// </summary>
    private void OnEventTriggered(object? sender, PetVisualState state)
    {
        _pet.AwakeIdleSeconds = 0; // 任何互動即歸零（冷落解除，§7.4.2）。

        switch (state)
        {
            case PetVisualState.Click:
                _happiness.TryAwardClickOrPlay(_pet); // +2，60 秒冷卻（§7.4.3）。
                break;

            case PetVisualState.Feed:
                PetCareActions.Feed(_pet);            // 降低飢餓度（§6.3）。
                _happiness.TryAwardFeed(_pet);        // +10，30 分冷卻（§7.4.3）。
                break;

            case PetVisualState.Sleep:
                if (_pet.Energy < PetCareActions.MaxValue)
                {
                    _sleeping = true;                 // 進入睡眠：後續 tick 回補能量至滿（§7.3.2）。
                }
                else
                {
                    // 已精神飽滿：不進入持續型睡眠（否則 durationSec=0 會無醒來條件而卡住），
                    // 也不發放「睡眠完成 +5」——避免在滿能量下反覆睡覺無冷卻刷幸福度（§7.4.3）。
                    _window.EndCurrentAnimationEvent();
                }

                break;
        }

        // 數值可能改變心情（如餵食讓 Hunger ≤ 70 脫離 SAD）；事件進行中仍由事件圖優先顯示。
        _window.SetMood(_mood.EvaluateVisualState(_pet));
    }

    /// <summary>
    /// 右鍵選單中「無對應視覺事件」的指令（§6.3，見 <see cref="UI.PetMenuAction"/>）。
    /// 「退出」由視窗直接處理（見 <see cref="MainWindow.OnExitMenuClick"/>），此處不重複。
    /// </summary>
    private void OnMenuActionRequested(object? sender, PetMenuAction action)
    {
        switch (action)
        {
            case PetMenuAction.Play:
                _pet.AwakeIdleSeconds = 0;
                _happiness.TryAwardClickOrPlay(_pet); // 玩耍與點擊共用 +2 / 60 秒冷卻（§7.4.3）。
                break;

            case PetMenuAction.Clean:
                // §6.3：清潔的數值效果設計檔未定義，暫僅歸零冷落計時（視為一次互動）。
                _pet.AwakeIdleSeconds = 0;
                break;

            case PetMenuAction.Settings:
            case PetMenuAction.About:
                // 設定 / 關於視窗屬 Phase 2（§14 規劃中），此處尚無副作用。
                break;

            case PetMenuAction.Exit:
                // 由 MainWindow.OnExitMenuClick 直接 Application.Shutdown（存檔於 App.Exit 掛點）。
                break;
        }
    }

    /// <summary>雙寵物互動回補（§6.5.4 / §7.4.3：+3，30 分冷卻，兩隻各自呼叫），並歸零冷落計時。</summary>
    /// <returns><c>true</c> = 冷卻已過並發放（可播互動素材）；<c>false</c> = 冷卻中，未加幸福度。</returns>
    public bool TryAwardInteraction()
    {
        _pet.AwakeIdleSeconds = 0; // 互動視為一次互動，解除冷落（§7.4.2）。
        return _happiness.TryAwardPetInteraction(_pet);
    }

    /// <summary>睡眠「醒來」：結束睡眠事件並發放「睡眠完成 +5」（§7.3.2 / §7.4.3）。</summary>
    private void WakeFromSleep()
    {
        _sleeping = false;
        _window.EndCurrentAnimationEvent();
        _happiness.AwardSleepComplete(_pet);
    }

    /// <summary>停止狀態 tick 並關閉視窗（供 E2 送走寵物 / 關閉時）。</summary>
    public void Dispose()
    {
        _stateTimer.Stop();
        _stateTimer.Tick -= OnStateTick;
        _window.EventTriggered -= OnEventTriggered;
        _window.MenuActionRequested -= OnMenuActionRequested;
        _window.Close();
    }
}
