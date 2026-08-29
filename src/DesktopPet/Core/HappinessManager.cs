using DesktopPet.Models;

namespace DesktopPet.Core;

/// <summary>
/// 一次幸福度衰減結算的結果（§7.4.2，每小時執行時間結算一次），供上層 / 測試觀察本 tick 實際發生的衰減。
/// </summary>
public readonly struct HappinessTickResult : IEquatable<HappinessTickResult>
{
    /// <summary>本 tick 是否觸發每小時的衰減結算。</summary>
    public bool Settled { get; init; }

    /// <summary>幸福度的實際變化量（未結算為 0；結算時為負；夾在 0 後可能為 0）。</summary>
    public int Delta { get; init; }

    /// <summary>本次結算採用的每小時衰減速率（1–5；未結算為 0）。供除錯 / 測試對照 §7.4.2 疊加表。</summary>
    public int DecayRate { get; init; }

    /// <summary>未結算（多數 tick 的情形）。</summary>
    public static HappinessTickResult NotSettled => default;

    public bool Equals(HappinessTickResult other) =>
        Settled == other.Settled && Delta == other.Delta && DecayRate == other.DecayRate;

    public override bool Equals(object? obj) => obj is HappinessTickResult r && Equals(r);
    public override int GetHashCode() => HashCode.Combine(Settled, Delta, DecayRate);
}

/// <summary>
/// 幸福度的衰減與回補（設計檔 §7.4.2 / §7.4.3）：採「自然衰減 + 操作回補」模型。
/// 長時間不餵食 / 不讓睡覺 / 不互動會慢慢扣減（<see cref="Tick"/>，每小時結算）；餵食、點擊 / 玩耍、
/// 睡眠完成、雙寵物互動則回補（<c>TryAward*</c> / <c>AwardSleepComplete</c>，帶冷卻）。
/// <b>幸福度是純數值指標，不影響外觀</b>（§7.2.1）：本類別只讀寫 <see cref="Pet.Happiness"/>
/// 與冷卻時間戳，<b>不碰</b> <c>Hunger</c> / <c>Energy</c> / <c>Health</c>（那些由 C1 的
/// <c>StateManager</c> 負責）。
/// </summary>
/// <remarks>
/// <b>核心不變量（§7.4.2 / §7.4.3，寫錯會讓凍結失效或冷卻失去意義）：</b>
/// <list type="number">
///   <item><description>冷落懲罰以 <see cref="Pet.AwakeIdleSeconds"/>（<b>累計秒數</b>）判定，
///     <b>不可</b>用 <see cref="Pet.LastInteractionTime"/> 與現在時刻的差值——否則關機三天重開會瞬間狂扣，
///     §7.4.4 凍結形同虛設。本類別只<b>唯讀</b>取用 <c>AwakeIdleSeconds</c>；其累加在 C1、歸零在互動層（D3）。</description></item>
///   <item><description><b>冷卻只 gate 幸福度加成，不 gate 操作本身</b>（§7.4.3）：冷卻期間動畫照播、音效照響、
///     <c>AwakeIdleSeconds</c> 照歸零（皆由互動層負責），本類別只是「這次不加幸福度」（回傳 <c>false</c>）。
///     不可實作成「冷卻中禁止餵食 / 點擊」。</description></item>
///   <item><description>幸福度夾在 <c>0 ~ 100</c>。</description></item>
/// </list>
/// <para>
/// <b>冷卻時間戳的語意（釐清 §7.4.6 的欄位重用）：</b>時間戳記的值一律代表「<b>該類回補上次實際發放的時刻</b>」，
/// <b>只在成功發放時</b>更新；冷卻中被呼叫時<b>不</b>更新（否則連續點擊會一直把 60 秒往後推、永遠拿不到 +2）。
/// <list type="bullet">
///   <item><description>餵食冷卻（30 分）用 <see cref="Pet.LastFedTime"/>（獨立）。</description></item>
///   <item><description>點擊 / 玩耍冷卻（60 秒）與雙寵物互動冷卻（30 分）<b>共用</b> <see cref="Pet.LastInteractionTime"/>
///     （§7.4.6「互動冷卻用 <c>LastInteractionTime</c>」）：其值 = 最近一次<b>任一種</b>互動回補的時刻，
///     兩者各自要求「距上次互動回補至少 N」。因此點擊後 30 分內不再發互動加成、互動後 60 秒內不再發點擊加成，
///     為刻意的防刷耦合。</description></item>
///   <item><description>睡眠完成 +5 <b>無冷卻</b>，不讀寫任何冷卻時間戳。</description></item>
/// </list>
/// 這與 §7.4.2 的示意碼「任何互動即 <c>LastInteractionTime = Now</c>」略有出入：該欄位的權威用途是
/// §7.4.3 冷卻（見 <see cref="Pet.LastInteractionTime"/> 註解），冷落解除改由 <c>AwakeIdleSeconds</c> 歸零表達，
/// 兩者刻意分離，避免「冷卻參考時刻」被每次操作重置而失效。
/// </para>
/// <para>
/// 純邏輯（不依賴 WPF，可跨平台單元測試）。因持有每小時衰減的秒數零頭，<b>每隻寵物請用獨立實例</b>
/// （與 C1 的 <c>StateManager</c> 同置於各自的 <c>PetInstance</c>）。零頭刻意<b>不</b>持久化
/// （§7.4.6 僅新增兩個累計欄位），重開歸零——比凍結更保守，非違反。
/// </para>
/// </remarks>
public sealed class HappinessManager
{
    // ── §7.4.2 衰減（每小時結算，可疊加）─────────────────────────────
    /// <summary>衰減結算週期（§7.4.2：以小時為單位；此為執行時間秒數）。</summary>
    public const int DecaySettleIntervalSeconds = 60 * 60;

    /// <summary>基礎自然衰減（恆常，每小時 -1）。</summary>
    public const int BaseDecayPerHour = 1;
    /// <summary>飢餓懲罰（<c>Hunger &gt; 70</c>，額外每小時 -1）。</summary>
    public const int HungerPenaltyPerHour = 1;
    /// <summary>疲勞懲罰（<c>Energy &lt; 20</c>，額外每小時 -1）。</summary>
    public const int EnergyPenaltyPerHour = 1;
    /// <summary>冷落懲罰（累計未互動超過 4 小時，額外每小時 -2）。</summary>
    public const int IdlePenaltyPerHour = 2;

    /// <summary>飢餓懲罰門檻（<b>嚴格大於</b>，與 §7.2.1 心情門檻同值）。</summary>
    public const int HungerPenaltyThreshold = 70;
    /// <summary>疲勞懲罰門檻（<b>嚴格小於</b>，與 §7.2.1 心情門檻同值）。</summary>
    public const int EnergyPenaltyThreshold = 20;
    /// <summary>冷落懲罰門檻（<b>嚴格大於</b>，§7.4.2：累計未互動「超過」4 小時）。</summary>
    public const int IdlePenaltyThresholdSeconds = 4 * 60 * 60;

    // ── §7.4.3 回補（增加值 + 冷卻）──────────────────────────────────
    /// <summary>餵食回補（+10）。</summary>
    public const int FeedHappiness = 10;
    /// <summary>點擊 / 玩耍回補（+2）。</summary>
    public const int ClickHappiness = 2;
    /// <summary>睡眠完成回補（+5，無冷卻）。</summary>
    public const int SleepHappiness = 5;
    /// <summary>雙寵物互動回補（+3，兩隻各自 +3）。</summary>
    public const int InteractionHappiness = 3;

    /// <summary>餵食冷卻（30 分鐘）。</summary>
    public static readonly TimeSpan FeedCooldown = TimeSpan.FromMinutes(30);
    /// <summary>點擊 / 玩耍冷卻（60 秒）。</summary>
    public static readonly TimeSpan ClickCooldown = TimeSpan.FromSeconds(60);
    /// <summary>雙寵物互動冷卻（30 分鐘）。</summary>
    public static readonly TimeSpan InteractionCooldown = TimeSpan.FromMinutes(30);

    /// <summary>數值下限（含）。</summary>
    public const int MinValue = 0;
    /// <summary>數值上限（含）。</summary>
    public const int MaxValue = 100;

    private readonly Func<DateTime> _now;
    private int _decayAccumSeconds; // 每小時衰減的秒數零頭（§7.4.6：刻意不持久化）。

    /// <param name="clock">時鐘（預設 <see cref="DateTime.Now"/>；與 <c>Last*Time</c> 等牆鐘時間戳一致）。可注入以利測試。</param>
    public HappinessManager(Func<DateTime>? clock = null)
    {
        _now = clock ?? (() => DateTime.Now);
    }

    /// <summary>當前累計的衰減零頭秒數（0 ~ <see cref="DecaySettleIntervalSeconds"/>-1；供除錯 / 測試）。</summary>
    public int DecayAccumSeconds => _decayAccumSeconds;

    /// <summary>
    /// 執行期狀態 tick（1 Hz）的幸福度衰減部分（§7.4.2）：累加 1 秒，滿 1 小時執行時間即依當下條件
    /// 結算一次疊加衰減（基礎 -1，飢餓 / 疲勞各額外 -1，冷落額外 -2），幸福度夾 0–100。
    /// 與 C1 的 <c>StateManager.Tick</c> 分工，兩者可各自於同一秒被呼叫（順序不影響：本方法只讀
    /// <c>Hunger</c> / <c>Energy</c> / <c>AwakeIdleSeconds</c>）。
    /// </summary>
    /// <param name="pet">受結算的寵物（僅就地修改 <see cref="Pet.Happiness"/>）。</param>
    public HappinessTickResult Tick(Pet pet)
    {
        ArgumentNullException.ThrowIfNull(pet);

        if (++_decayAccumSeconds < DecaySettleIntervalSeconds)
            return HappinessTickResult.NotSettled;

        _decayAccumSeconds -= DecaySettleIntervalSeconds;
        int rate = ComputeHourlyDecay(pet);
        int delta = ApplyHappiness(pet, -rate);
        return new HappinessTickResult { Settled = true, Delta = delta, DecayRate = rate };
    }

    /// <summary>
    /// 依 §7.4.2 疊加表計算「當下條件」的每小時衰減速率（1–5）：基礎 1，
    /// <c>Hunger&gt;70</c> +1、<c>Energy&lt;20</c> +1、<c>AwakeIdleSeconds&gt;4h</c> +2。
    /// 純函式，方便單獨驗證疊加邏輯。
    /// </summary>
    public static int ComputeHourlyDecay(Pet pet)
    {
        ArgumentNullException.ThrowIfNull(pet);

        int rate = BaseDecayPerHour;
        if (pet.Hunger > HungerPenaltyThreshold) rate += HungerPenaltyPerHour;
        if (pet.Energy < EnergyPenaltyThreshold) rate += EnergyPenaltyPerHour;
        if (pet.AwakeIdleSeconds > IdlePenaltyThresholdSeconds) rate += IdlePenaltyPerHour;
        return rate;
    }

    /// <summary>
    /// 餵食回補（§7.4.3：+10，冷卻 30 分鐘，冷卻參考 <see cref="Pet.LastFedTime"/>）。
    /// </summary>
    /// <returns><c>true</c> = 冷卻已過並發放（幸福度可能夾在 100）；<c>false</c> = 冷卻中，未加幸福度。</returns>
    public bool TryAwardFeed(Pet pet, DateTime? now = null)
    {
        ArgumentNullException.ThrowIfNull(pet);
        return TryAward(pet, now ?? _now(), p => p.LastFedTime, (p, t) => p.LastFedTime = t, FeedCooldown, FeedHappiness);
    }

    /// <summary>
    /// 點擊 / 玩耍回補（§7.4.3：+2，冷卻 60 秒，冷卻參考 <see cref="Pet.LastInteractionTime"/>）。
    /// </summary>
    /// <returns><c>true</c> = 冷卻已過並發放；<c>false</c> = 冷卻中，未加幸福度（操作本身仍照常，由互動層負責）。</returns>
    public bool TryAwardClickOrPlay(Pet pet, DateTime? now = null)
    {
        ArgumentNullException.ThrowIfNull(pet);
        return TryAward(pet, now ?? _now(), p => p.LastInteractionTime, (p, t) => p.LastInteractionTime = t, ClickCooldown, ClickHappiness);
    }

    /// <summary>
    /// 雙寵物互動回補（§7.4.3 / §6.5.4：+3，冷卻 30 分鐘，冷卻參考 <see cref="Pet.LastInteractionTime"/>；
    /// 兩隻各自呼叫）。與點擊共用 <c>LastInteractionTime</c>（見類別註解的冷卻語意）。
    /// </summary>
    /// <returns><c>true</c> = 冷卻已過並發放；<c>false</c> = 冷卻中，未加幸福度。</returns>
    public bool TryAwardPetInteraction(Pet pet, DateTime? now = null)
    {
        ArgumentNullException.ThrowIfNull(pet);
        return TryAward(pet, now ?? _now(), p => p.LastInteractionTime, (p, t) => p.LastInteractionTime = t, InteractionCooldown, InteractionHappiness);
    }

    /// <summary>
    /// 睡眠完成回補（§7.4.3：+5，<b>無冷卻</b>）。由 <c>SLEEP</c> 事件在 <c>Energy</c> 回滿時呼叫（§7.3.2）。
    /// 不讀寫任何冷卻時間戳。
    /// </summary>
    /// <returns>幸福度的實際變化量（夾 0–100 後，可能為 0）。</returns>
    public int AwardSleepComplete(Pet pet)
    {
        ArgumentNullException.ThrowIfNull(pet);
        return ApplyHappiness(pet, SleepHappiness);
    }

    /// <summary>
    /// 冷卻閘門：距上次發放（<paramref name="getStamp"/>）已滿 <paramref name="cooldown"/> 才發放
    /// <paramref name="amount"/> 並把時間戳更新為 <paramref name="now"/>；否則不動幸福度也不動時間戳。
    /// 首次呼叫時時間戳為 <c>default(DateTime)</c>，差值極大必定通過。
    /// </summary>
    private static bool TryAward(Pet pet, DateTime now, Func<Pet, DateTime> getStamp, Action<Pet, DateTime> setStamp, TimeSpan cooldown, int amount)
    {
        if (now - getStamp(pet) < cooldown)
            return false; // 冷卻中：不加幸福度，時間戳不動（避免把冷卻一直往後推）。

        ApplyHappiness(pet, amount);
        setStamp(pet, now);
        return true;
    }

    /// <summary>對 <see cref="Pet.Happiness"/> 施加 <paramref name="step"/> 並夾 0–100，回傳夾住後的實際變化量。</summary>
    private static int ApplyHappiness(Pet pet, int step)
    {
        int before = pet.Happiness;
        int after = Math.Clamp(before + step, MinValue, MaxValue);
        if (after != before) pet.Happiness = after;
        return after - before;
    }
}
