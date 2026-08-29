namespace DesktopPet.Models;

/// <summary>
/// 執行期的整體狀態聚合根（設計檔 §5.1）。相較舊版的單一 <c>CurrentPet</c>，
/// 改以 <see cref="Pets"/> 清單承載 1–2 隻寵物（§5.2）：長度為 1 即單寵物模式，
/// 長度為 2 時互動邏輯才啟動（§6.5）。
/// </summary>
/// <remarks>
/// §5.1 原以 <c>Dictionary&lt;string, object&gt;</c> 承載設定，此處升級為強型別
/// <see cref="Models.Settings"/>（§12.4 / §14）。實際持久化時如何切分到
/// <c>pet_data.json</c> / <c>settings.json</c> / <c>achievements.json</c>（§8.1）由 A3 決定。
/// </remarks>
public class GameState
{
    /// <summary>目前飼養的寵物清單（1–2 隻）。</summary>
    public List<Pet> Pets { get; set; } = new();

    /// <summary>飼養上限（固定為 2，未來可調整）。</summary>
    public int MaxPetSlots { get; set; } = 2;

    /// <summary>成就進度（成就 ID → 進度值）。</summary>
    public Dictionary<string, int> Achievements { get; set; } = new();

    /// <summary>應用程式設定（含全域音效與語言）。</summary>
    public Settings Settings { get; set; } = new();
}
