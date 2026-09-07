using DesktopPet.Models;

namespace DesktopPet.Core;

/// <summary>
/// 右鍵照顧動作對「四項數值」的實際效果（E4，設計檔 §6.3 / §7.3.2 / §7.4）。純邏輯、無狀態，
/// 可跨平台單元測試。<b>只碰 <c>Hunger</c> / <c>Energy</c></b>；幸福度回補由
/// <see cref="HappinessManager"/> 負責（§7.4.3），本類別不重複發放幸福度。
/// </summary>
/// <remarks>
/// <b>數值取捨（設計檔留白處，非權威）：</b>設計檔 §7.4.3 明列了各操作的<b>幸福度</b>回補與冷卻，
/// 但<b>未</b>指定「餵食扣多少飢餓」與「睡眠回能量的速率」。此處採以下可調預設值，集中為具名常數方便日後平衡：
/// <list type="bullet">
///   <item><description><see cref="FeedHungerRelief"/>：餵食一次 <c>Hunger -30</c>（約抵銷 90 分鐘的自然飢餓累積）。</description></item>
///   <item><description><see cref="SleepEnergyGainPerSecond"/>：睡眠期間每個 1 Hz 狀態 tick <c>Energy +1</c>，
///     回滿（100）即由上層結束睡眠並發放 §7.4.3 的「睡眠完成 +5」。</description></item>
/// </list>
/// 這兩個數值不寫入存檔、不屬核心不變量，改動不影響其他模組。
/// </remarks>
public static class PetCareActions
{
    /// <summary>數值下限（含），與 §7.4.1 一致。</summary>
    public const int MinValue = 0;

    /// <summary>數值上限（含），與 §7.4.1 一致。</summary>
    public const int MaxValue = 100;

    /// <summary>餵食一次降低的飢餓度（可調；設計檔未指定，見類別註解）。</summary>
    public const int FeedHungerRelief = 30;

    /// <summary>睡眠期間每個 1 Hz 狀態 tick 回補的能量（可調；設計檔未指定，見類別註解）。</summary>
    public const int SleepEnergyGainPerSecond = 1;

    /// <summary>
    /// 餵食（§6.3 右鍵「餵食」）：降低飢餓度 <see cref="FeedHungerRelief"/>，夾在 0–100。
    /// <b>不</b>處理幸福度（由 <see cref="HappinessManager.TryAwardFeed"/> 帶冷卻發放）。
    /// </summary>
    /// <param name="pet">受影響的寵物（就地修改 <see cref="Pet.Hunger"/>）。</param>
    /// <returns>飢餓度的實際變化量（≤ 0；已為 0 時回傳 0）。</returns>
    public static int Feed(Pet pet)
    {
        ArgumentNullException.ThrowIfNull(pet);

        int before = pet.Hunger;
        int after = Math.Clamp(before - FeedHungerRelief, MinValue, MaxValue);
        if (after != before) pet.Hunger = after;
        return after - before;
    }

    /// <summary>
    /// 睡眠期間的一次能量回補（§7.3.2「睡眠持續至醒來」）：<c>Energy +<see cref="SleepEnergyGainPerSecond"/></c>，
    /// 夾在 0–100。由上層在睡眠事件進行中的每個 1 Hz 狀態 tick 呼叫；回滿即代表「醒來」條件達成
    /// （上層據此結束睡眠事件並發放 §7.4.3 的「睡眠完成 +5」）。
    /// </summary>
    /// <param name="pet">受影響的寵物（就地修改 <see cref="Pet.Energy"/>）。</param>
    /// <returns><c>true</c> = 能量已回滿（<see cref="MaxValue"/>），代表可醒來；<c>false</c> = 尚未滿。</returns>
    public static bool RecoverEnergyForSleep(Pet pet)
    {
        ArgumentNullException.ThrowIfNull(pet);

        pet.Energy = Math.Clamp(pet.Energy + SleepEnergyGainPerSecond, MinValue, MaxValue);
        return pet.Energy >= MaxValue;
    }
}
