namespace DesktopPet.Models;

/// <summary>
/// 寵物心情（設計檔 §5.1 / §7.2.1）。判定只看 <c>Hunger</c> 與 <c>Energy</c>，
/// 順序不可調換：先 <c>Hunger &gt; 70 → Sad</c>，再 <c>Energy &lt; 20 → LowEnergy</c>，否則 <c>Neutral</c>。
/// </summary>
/// <remarks>
/// 存檔時以「字串」序列化（例：<c>LowEnergy → "LOW_ENERGY"</c>）。字串對應由 A3
/// 於 <c>StorageManager</c> 註冊 <c>JsonStringEnumConverter</c> + 自訂命名策略統一處理，
/// 本列舉不掛任何序列化屬性。列舉宣告順序沿用 §5.1，數值不用於持久化。
/// </remarks>
public enum PetMood
{
    Sad,        // 飢餓：Hunger > 70
    LowEnergy,  // 低能量：Energy < 20
    Neutral     // 一般：以上皆非
}
