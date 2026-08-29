namespace DesktopPet.Core.Visuals;

/// <summary>
/// 寵物「視覺狀態」列舉（設計檔 §7.3 / §7.3.5）：決定當前該播哪一類動畫單元。
/// </summary>
/// <remarks>
/// 共 6 態 = 3 類「心情圖片」+ 3 類「事件圖片」：
/// <list type="bullet">
///   <item><description>心情（由 §7.2.1 依 <c>Hunger</c>/<c>Energy</c> 自動判定）：
///     <see cref="Neutral"/>、<see cref="Sad"/>、<see cref="LowEnergy"/>。</description></item>
///   <item><description>事件（由使用者操作觸發）：<see cref="Click"/>、<see cref="Feed"/>、<see cref="Sleep"/>。</description></item>
/// </list>
/// 三個心情態與 <see cref="DesktopPet.Models.PetMood"/> 一一對應（見
/// <see cref="MoodEvaluator.ToVisualState(DesktopPet.Models.PetMood)"/>）；三個事件態沒有對應心情。
/// 事件圖片優先權高於心情圖片，事件播畢後重跑 §7.2.1 回到對應心情（§7.3.2）。
/// <para>
/// 宣告順序沿用設計檔 §7.3.5，供 <c>PetVisualSelector</c>／<c>VisualRegistry</c>（B4/B5）作為索引鍵。
/// 此列舉純作執行期狀態鍵使用，<b>不進存檔</b>——存檔僅持久化 3 值的 <see cref="DesktopPet.Models.PetMood"/>。
/// </para>
/// </remarks>
public enum PetVisualState
{
    // ── 心情圖片（§7.2.1 自動判定；與 PetMood 對應）─────────────────
    Neutral,    // 一般：Hunger ≤ 70 且 Energy ≥ 20（anim_idle_*）
    Sad,        // 飢餓：Hunger > 70（anim_sad_*）
    LowEnergy,  // 低能量：Energy < 20（anim_tired_*）

    // ── 事件圖片（§7.3.2 使用者操作觸發）──────────────────────────
    Click,      // 點擊寵物（anim_click_*）
    Feed,       // 右鍵選單「餵食」（anim_feed_*）
    Sleep       // 右鍵選單「睡眠」／自動入睡（anim_sleep_*）
}
