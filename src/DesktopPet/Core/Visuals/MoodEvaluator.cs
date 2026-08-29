using DesktopPet.Models;

namespace DesktopPet.Core.Visuals;

/// <summary>
/// 心情判定器（設計檔 §7.2.1）：由寵物的 <c>Hunger</c> / <c>Energy</c> 推導當前心情。
/// </summary>
/// <remarks>
/// <b>核心不變量（§7.2.1，寫錯會產生難以察覺的 bug）：</b>
/// <list type="number">
///   <item><description>只看 <c>Hunger</c> 與 <c>Energy</c>，<b>完全不看 <c>Happiness</c></b>——
///     幸福度是純數值指標，與外觀解耦（面板顯示 90% 卻是 <c>NEUTRAL</c> 圖是正常的）。</description></item>
///   <item><description>三分支，<b>判定順序不可調換</b>：先 <c>Hunger &gt; 70 → SAD</c>，
///     再 <c>Energy &lt; 20 → LOW_ENERGY</c>，否則 <c>NEUTRAL</c>。
///     兩條件同時成立時飢餓優先，顯示 <c>SAD</c>。</description></item>
/// </list>
/// 本類別無狀態，可安全共用單一實例。門檻採「設計檔字面條件」：<c>&gt; 70</c>、<c>&lt; 20</c>
/// （即 <c>Hunger == 70</c> 不算 SAD、<c>Energy == 20</c> 不算 LOW_ENERGY）。
/// </remarks>
public sealed class MoodEvaluator
{
    /// <summary>飢餓度 SAD 門檻（<b>嚴格大於</b>，§7.2.1 / §7.3.1）。</summary>
    public const int HungerSadThreshold = 70;

    /// <summary>能量 LOW_ENERGY 門檻（<b>嚴格小於</b>，§7.2.1 / §7.3.1）。</summary>
    public const int EnergyLowThreshold = 20;

    /// <summary>
    /// 依 §7.2.1 三分支判定寵物心情。順序：<c>Hunger &gt; 70</c> → <see cref="PetMood.Sad"/>，
    /// <c>Energy &lt; 20</c> → <see cref="PetMood.LowEnergy"/>，否則 <see cref="PetMood.Neutral"/>。
    /// </summary>
    /// <param name="pet">受評的寵物（僅讀取 <c>Hunger</c> / <c>Energy</c>）。</param>
    /// <returns>3 值心情列舉，供存檔與面板顯示。</returns>
    public PetMood EvaluateMood(Pet pet)
    {
        ArgumentNullException.ThrowIfNull(pet);

        if (pet.Hunger > HungerSadThreshold) return PetMood.Sad;
        if (pet.Energy < EnergyLowThreshold) return PetMood.LowEnergy;
        return PetMood.Neutral;
    }

    /// <summary>
    /// 便利方法：直接回傳心情對應的 <see cref="PetVisualState"/>（供 §7.1 狀態 tick 第 2→3 步、
    /// <c>PetVisualSelector</c>（B5）取用）。等同 <c>ToVisualState(EvaluateMood(pet))</c>。
    /// </summary>
    /// <remarks>只會回傳三個「心情態」；事件態（Click/Feed/Sleep）由使用者操作觸發，不經此判定。</remarks>
    public PetVisualState EvaluateVisualState(Pet pet) => ToVisualState(EvaluateMood(pet));

    /// <summary>
    /// 將 3 值 <see cref="PetMood"/> 映射為對應的 <see cref="PetVisualState"/>（一一對應）。
    /// 這是心情層（§7.2.1，產出 <see cref="PetMood"/>）與視覺層（§7.3，消費 <see cref="PetVisualState"/>）
    /// 之間的唯一橋接點，避免兩處各自用列舉名硬轉。
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mood"/> 非三個心情值之一。</exception>
    public static PetVisualState ToVisualState(PetMood mood) => mood switch
    {
        PetMood.Neutral => PetVisualState.Neutral,
        PetMood.Sad => PetVisualState.Sad,
        PetMood.LowEnergy => PetVisualState.LowEnergy,
        _ => throw new ArgumentOutOfRangeException(nameof(mood), mood, "未知的 PetMood 值。")
    };
}
