using DesktopPet.Models;

namespace DesktopPet.Core;

/// <summary>
/// 離線凍結處理（設計檔 §7.4.4 / §7.1 啟動步驟 0）：程式關閉期間<b>四項數值全部凍結不變</b>
/// （<c>Hunger</c> / <c>Energy</c> / <c>Happiness</c> / <c>Health</c>），<b>不做任何離線補算</b>；
/// 啟動時（載入存檔後、狀態 tick 開跑前）呼叫一次，<b>僅將 <see cref="Pet.LastTickTime"/> 重設為現在時刻</b>，
/// 作為執行期狀態 tick 的基準。重開後寵物狀態與關閉當下完全相同，如同時間未曾流逝。
/// </summary>
/// <remarks>
/// <b>核心不變量（§7.4.4，寫錯會讓凍結形同虛設）：</b>
/// <list type="bullet">
///   <item><description>四項數值一律<b>不動</b>；§7.4.1 的 <c>Hunger</c> / <c>Energy</c> 自然變化、
///     §7.4.2 幸福度衰減、§7.4.3 回補、§7.4.5 健康度結算，離線期間<b>全部略過</b>（不在此補算）。</description></item>
///   <item><description><see cref="Pet.AwakeIdleSeconds"/> / <see cref="Pet.HealthCheckSeconds"/>
///     <b>維持存檔值、不動</b>——它們本就只在執行期累加，離線時間不使其增加，也<b>不</b>在此歸零
///     （否則關機會重置冷落 / 健康度計時進度）。</description></item>
///   <item><description>唯一副作用是重設 <see cref="Pet.LastTickTime"/>。因離線期間不累計任何變化，
///     <b>改系統時鐘也無從刷數值</b>，故不需額外的時鐘回調防護或 24 小時上限。</description></item>
/// </list>
/// <para>純邏輯（不依賴 WPF），可跨平台單元測試。無狀態，可安全共用單一實例。</para>
/// </remarks>
public sealed class OfflineFreezeHandler
{
    private readonly Func<DateTime> _now;

    /// <param name="clock">
    /// 時鐘（預設 <see cref="DateTime.Now"/>；與 <see cref="Pet.LastTickTime"/> 等牆鐘時間戳一致）。
    /// 供 <see cref="Apply(Pet)"/> / <see cref="Apply(IEnumerable{Pet})"/> 取用，可注入以利測試。
    /// </param>
    public OfflineFreezeHandler(Func<DateTime>? clock = null)
    {
        _now = clock ?? (() => DateTime.Now);
    }

    /// <summary>
    /// 對單一寵物套用離線凍結：四項數值與兩個累計欄位<b>全部保持不變</b>，僅將
    /// <see cref="Pet.LastTickTime"/> 重設為 <paramref name="now"/>。
    /// </summary>
    /// <param name="pet">要重設 tick 基準的寵物（就地修改）。</param>
    /// <param name="now">重設後的基準時刻（通常為現在時刻）。</param>
    public void Apply(Pet pet, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(pet);

        // Hunger / Energy / Happiness / Health、AwakeIdleSeconds / HealthCheckSeconds 全部保持不變。
        pet.LastTickTime = now; // 唯一副作用：重設執行期狀態 tick 的基準。
    }

    /// <summary>以本處理器的時鐘取現在時刻，對單一寵物套用離線凍結。</summary>
    public void Apply(Pet pet) => Apply(pet, _now());

    /// <summary>
    /// 對整批寵物（<c>GameState.Pets</c>，1–2 隻）套用離線凍結，共用同一個現在時刻，
    /// 讓多隻的 tick 基準一致。
    /// </summary>
    /// <param name="pets">要重設 tick 基準的寵物集合。</param>
    public void Apply(IEnumerable<Pet> pets)
    {
        ArgumentNullException.ThrowIfNull(pets);

        var now = _now();
        foreach (var pet in pets)
            Apply(pet, now);
    }
}
