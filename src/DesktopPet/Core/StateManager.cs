using DesktopPet.Models;

namespace DesktopPet.Core;

/// <summary>
/// 一次狀態 tick 的結算結果（§7.1 狀態 tick 第 1 步的產出），供上層（E4）或測試觀察本 tick
/// 實際發生的變化，避免呼叫端再自行比對 <see cref="Pet"/> 前後值。所有 delta 皆為「夾在
/// 0–100 之後」的實際變化量（例：<c>Hunger</c> 已達 100 時再 +1，<see cref="HungerDelta"/> 為 0）。
/// </summary>
public readonly struct StateTickResult : IEquatable<StateTickResult>
{
    /// <summary>本 tick 飢餓度的實際變化量（0 或 +1；夾住後可能為 0）。</summary>
    public int HungerDelta { get; init; }

    /// <summary>本 tick 能量的實際變化量（0 或 -1；夾住後可能為 0）。</summary>
    public int EnergyDelta { get; init; }

    /// <summary>本 tick 是否觸發健康度結算（每 30 分鐘執行時間一次，§7.4.5）。</summary>
    public bool HealthSettled { get; init; }

    /// <summary>健康度結算的實際變化量（未結算時為 0；結算時為 -1 / 0 / +1，夾住後可能為 0）。</summary>
    public int HealthDelta { get; init; }

    /// <summary>本 tick 是否有任何四項數值改變（供上層決定是否需要重繪面板 / 重新判定心情）。</summary>
    public bool AnyValueChanged => HungerDelta != 0 || EnergyDelta != 0 || HealthDelta != 0;

    public bool Equals(StateTickResult other) =>
        HungerDelta == other.HungerDelta && EnergyDelta == other.EnergyDelta &&
        HealthSettled == other.HealthSettled && HealthDelta == other.HealthDelta;

    public override bool Equals(object? obj) => obj is StateTickResult r && Equals(r);
    public override int GetHashCode() => HashCode.Combine(HungerDelta, EnergyDelta, HealthSettled, HealthDelta);
}

/// <summary>
/// 四項狀態數值的執行期結算器（設計檔 §7.1 狀態 tick 第 1 步 + §7.4.1 / §7.4.5）：
/// 固定 <b>1 Hz</b> 被驅動，負責 <c>Hunger</c> / <c>Energy</c> 的自然變化與健康度的週期結算，
/// 並累加冷落 / 健康度計時。<b>本類別不碰 <c>Happiness</c></b>（衰減 / 回補由 C2 的
/// <c>HappinessManager</c> 負責，§7.4.2 / §7.4.3），僅在健康度結算時<b>唯讀</b>取用其值。
/// </summary>
/// <remarks>
/// <b>核心不變量（§7.4，寫錯會產生難以察覺的 bug）：</b>
/// <list type="number">
///   <item><description><c>Hunger</c> <b>越高越餓、隨時間遞增</b>（每 3 分鐘 +1）。寫成遞減會使
///     <c>Hunger &gt; 70</c> 的 <c>SAD</c> 分支永遠觸發不到（§7.1 警語）。</description></item>
///   <item><description>冷落懲罰以 <see cref="Pet.AwakeIdleSeconds"/> <b>累計秒數</b>判定，
///     <b>不可</b>用 <see cref="Pet.LastInteractionTime"/> 與現在時刻的差值——否則關機三天重開會瞬間
///     狂扣幸福度，凍結形同虛設（§7.4.2）。本類別只負責<b>累加</b>；歸零由互動處理層（D3/E4）執行。</description></item>
///   <item><description>四項數值一律夾在 <c>0 ~ 100</c>。</description></item>
/// </list>
/// <para>
/// <b>1 Hz 的意義與凍結：</b><see cref="Tick"/> 每次呼叫代表「執行期經過 1 秒」，一律 +1 秒推進，
/// <b>不</b>依真實時鐘差值補算。因此程式關閉、機器休眠（1 Hz 計時器不觸發）期間自然不推進，
/// 天然符合 §7.4.4 的凍結；改系統時鐘也無從刷數值。<c>Hunger</c> / <c>Energy</c> 的「不足一個
/// 週期」的零頭刻意<b>不</b>持久化（§7.4.6 僅新增 <c>AwakeIdleSeconds</c> / <c>HealthCheckSeconds</c>
/// 兩個累計欄位），重開時零頭歸零——比凍結更保守，非違反。
/// </para>
/// <para>
/// <b>職責分工：</b>本類別為<b>純邏輯</b>（不依賴 WPF，可跨平台單元測試）。實際的 1 Hz
/// <c>DispatcherTimer</c> 由 D 群 / E4 建立並每秒呼叫一次 <see cref="Tick"/>。因持有
/// <c>Hunger</c> / <c>Energy</c> 的週期零頭，<b>每隻寵物請用獨立的 <see cref="StateManager"/> 實例</b>。
/// </para>
/// </remarks>
public sealed class StateManager
{
    /// <summary>飢餓度 +1 的週期（§7.4.1：每 3 分鐘）。</summary>
    public const int HungerIntervalSeconds = 3 * 60;

    /// <summary>能量 -1 的週期（§7.4.1：每 5 分鐘）。</summary>
    public const int EnergyIntervalSeconds = 5 * 60;

    /// <summary>健康度結算的週期（§7.4.5：每 30 分鐘執行時間）。</summary>
    public const int HealthCheckIntervalSeconds = 30 * 60;

    /// <summary>數值下限（含）。</summary>
    public const int MinValue = 0;

    /// <summary>數值上限（含）。</summary>
    public const int MaxValue = 100;

    // ── §7.4.5 健康度結算的條件門檻（沿用設計檔字面條件，嚴格比較）─────────
    /// <summary>健康度 -1 的飢餓條件（<c>Hunger &gt; 90</c>）。</summary>
    public const int HealthDropHungerOver = 90;
    /// <summary>健康度 -1 的能量條件（<c>Energy &lt; 10</c>）。</summary>
    public const int HealthDropEnergyUnder = 10;
    /// <summary>健康度 -1 的幸福度條件（<c>Happiness &lt; 20</c>）。</summary>
    public const int HealthDropHappinessUnder = 20;
    /// <summary>健康度 +1 的飢餓條件（<c>Hunger &lt; 30</c>）。</summary>
    public const int HealthRiseHungerUnder = 30;
    /// <summary>健康度 +1 的能量條件（<c>Energy &gt; 70</c>）。</summary>
    public const int HealthRiseEnergyOver = 70;
    /// <summary>健康度 +1 的幸福度條件（<c>Happiness &gt; 70</c>）。</summary>
    public const int HealthRiseHappinessOver = 70;

    private readonly Func<DateTime> _now;

    // Hunger / Energy 的「不足一個週期」零頭（§7.4.6：刻意不持久化，只在執行期累加）。
    private int _hungerAccumSeconds;
    private int _energyAccumSeconds;

    /// <param name="clock">
    /// 時鐘（預設 <see cref="DateTime.Now"/>）。僅用來寫入 <see cref="Pet.LastTickTime"/>
    /// （與 <see cref="Pet.LastFedTime"/> / <see cref="Pet.LastInteractionTime"/> 同為本地牆鐘時間）；
    /// 數值推進本身不看時鐘差值（見類別註解的凍結說明）。可注入以利測試。
    /// </param>
    public StateManager(Func<DateTime>? clock = null)
    {
        _now = clock ?? (() => DateTime.Now);
    }

    /// <summary>當前累計的飢餓零頭秒數（0 ~ <see cref="HungerIntervalSeconds"/>-1；供除錯 / 測試觀察）。</summary>
    public int HungerAccumSeconds => _hungerAccumSeconds;

    /// <summary>當前累計的能量零頭秒數（0 ~ <see cref="EnergyIntervalSeconds"/>-1；供除錯 / 測試觀察）。</summary>
    public int EnergyAccumSeconds => _energyAccumSeconds;

    /// <summary>
    /// 執行期狀態 tick（§7.1，固定 1 Hz）：推進 1 秒的四項數值變化。步驟：
    /// <list type="number">
    ///   <item><description>累加飢餓零頭，滿 3 分鐘則 <c>Hunger +1</c>（夾 0–100）。</description></item>
    ///   <item><description>累加能量零頭，滿 5 分鐘則 <c>Energy -1</c>（夾 0–100）。</description></item>
    ///   <item><description><see cref="Pet.AwakeIdleSeconds"/> +1（冷落計時，§7.4.2；歸零由互動層負責）。</description></item>
    ///   <item><description><see cref="Pet.HealthCheckSeconds"/> +1，滿 30 分鐘則結算健康度（§7.4.5）。</description></item>
    ///   <item><description>更新 <see cref="Pet.LastTickTime"/> 為現在時刻。</description></item>
    /// </list>
    /// <b>不</b>觸碰 <c>Happiness</c>（C2 負責）；健康度結算只<b>唯讀</b>取用 <c>Happiness</c>。
    /// </summary>
    /// <param name="pet">受結算的寵物（就地修改）。每隻請配一個 <see cref="StateManager"/> 實例。</param>
    /// <returns>本 tick 的實際變化（見 <see cref="StateTickResult"/>）。</returns>
    public StateTickResult Tick(Pet pet)
    {
        ArgumentNullException.ThrowIfNull(pet);

        int hungerDelta = 0;
        int energyDelta = 0;

        // 1. 飢餓度：每 3 分鐘 +1（越高越餓，遞增）。
        if (++_hungerAccumSeconds >= HungerIntervalSeconds)
        {
            _hungerAccumSeconds -= HungerIntervalSeconds;
            hungerDelta = Apply(pet, p => p.Hunger, (p, v) => p.Hunger = v, +1);
        }

        // 2. 能量：每 5 分鐘 -1（越低越累，遞減）。
        if (++_energyAccumSeconds >= EnergyIntervalSeconds)
        {
            _energyAccumSeconds -= EnergyIntervalSeconds;
            energyDelta = Apply(pet, p => p.Energy, (p, v) => p.Energy = v, -1);
        }

        // 3. 冷落計時：只累加，歸零由互動層負責（§7.4.2）。
        pet.AwakeIdleSeconds++;

        // 4. 健康度結算：每 30 分鐘執行時間一次（§7.4.5）。
        bool healthSettled = false;
        int healthDelta = 0;
        if (++pet.HealthCheckSeconds >= HealthCheckIntervalSeconds)
        {
            pet.HealthCheckSeconds -= HealthCheckIntervalSeconds;
            healthSettled = true;
            healthDelta = SettleHealth(pet);
        }

        // 5. 更新狀態結算基準（供離線凍結的下次重設對照；只是牆鐘時間，不參與推進）。
        pet.LastTickTime = _now();

        return new StateTickResult
        {
            HungerDelta = hungerDelta,
            EnergyDelta = energyDelta,
            HealthSettled = healthSettled,
            HealthDelta = healthDelta,
        };
    }

    /// <summary>
    /// §7.4.5 健康度結算（僅在滿 30 分鐘時呼叫）。條件互斥、順序不影響結果：
    /// <c>Hunger&gt;90 || Energy&lt;10 || Happiness&lt;20 → -1</c>；
    /// 否則 <c>Hunger&lt;30 &amp;&amp; Energy&gt;70 &amp;&amp; Happiness&gt;70 → +1</c>；否則不變。
    /// </summary>
    /// <returns>健康度的實際變化量（夾 0–100 後，可能為 0）。</returns>
    private static int SettleHealth(Pet pet)
    {
        int step;
        if (pet.Hunger > HealthDropHungerOver || pet.Energy < HealthDropEnergyUnder || pet.Happiness < HealthDropHappinessUnder)
            step = -1;
        else if (pet.Hunger < HealthRiseHungerUnder && pet.Energy > HealthRiseEnergyOver && pet.Happiness > HealthRiseHappinessOver)
            step = +1;
        else
            return 0; // 一般狀態：健康度不變。

        return Apply(pet, p => p.Health, (p, v) => p.Health = v, step);
    }

    /// <summary>
    /// 對某一數值施加 <paramref name="step"/> 並夾在 0–100，回傳夾住後的<b>實際</b>變化量
    /// （例：已達上/下限時為 0）。以委派讀寫欄位，避免四項各寫一份夾住樣板。
    /// </summary>
    private static int Apply(Pet pet, Func<Pet, int> get, Action<Pet, int> set, int step)
    {
        int before = get(pet);
        int after = Math.Clamp(before + step, MinValue, MaxValue);
        if (after != before) set(pet, after);
        return after - before;
    }
}
