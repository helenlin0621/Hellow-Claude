namespace DesktopPet.Core.Visuals;

/// <summary>圖片類型的種類（設計檔 §7.3.3）：心情圖（自動判定）或事件圖（操作觸發）。</summary>
public enum VisualKind
{
    /// <summary>心情圖片：由 §7.2.1 依 <c>Hunger</c>/<c>Energy</c> 自動判定。</summary>
    Mood,

    /// <summary>事件圖片：由使用者操作觸發（點擊／餵食／睡眠）。</summary>
    Event,
}

/// <summary>
/// 一種視覺類型的登記定義（設計檔 §7.3.3 <c>pet_visuals.json</c> 的一筆條目，解析後的記憶體形式）。
/// </summary>
/// <remarks>
/// <c>code</c>／<c>fallback</c> 於載入時由 <see cref="VisualRegistry"/> 從 UPPER_SNAKE 字串
/// （<c>"LOW_ENERGY"</c>）對映為 <see cref="PetVisualState"/>；<c>kind</c> 由 <c>"mood"/"event"</c>
/// 對映為 <see cref="VisualKind"/>。<b>心情代號與檔名前綴不是一對一</b>（<c>LOW_ENERGY → anim_tired</c>），
/// 故 <see cref="Prefix"/> 必須來自設定檔，不可用列舉名轉小寫推導（§7.3.3）。
/// </remarks>
public sealed class VisualTypeDefinition
{
    /// <summary>狀態代號，對應 <see cref="PetVisualState"/>。</summary>
    public PetVisualState State { get; init; }

    /// <summary>心情圖或事件圖。</summary>
    public VisualKind Kind { get; init; }

    /// <summary>檔名前綴（例：<c>anim_tired</c>）。掃描 <c>{prefix}_*.png</c> 決定該狀態有哪些單元。</summary>
    public string Prefix { get; init; } = string.Empty;

    /// <summary>是否必填。設計檔中僅 <c>NEUTRAL</c> 為 <c>true</c>（§7.3.4）。</summary>
    public bool Required { get; init; }

    /// <summary>缺圖時退回的狀態；<c>null</c> 代表不換圖、維持目前畫面（§7.3.4，如 CLICK/FEED）。</summary>
    public PetVisualState? Fallback { get; init; }

    /// <summary>事件持續秒數（僅事件圖）。<c>0</c> 代表持續型（睡眠，直到條件解除）。語意為「至少 N 秒」（§7.3.2）。</summary>
    public double DurationSec { get; init; }

    /// <summary>多單元時的重抽間隔秒數（§7.3.5）。<c>0</c> 代表進入狀態時抽一次就不再重抽。</summary>
    public int RerollIntervalSec { get; init; }
}
