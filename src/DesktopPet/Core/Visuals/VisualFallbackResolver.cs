namespace DesktopPet.Core.Visuals;

/// <summary>
/// 缺圖時的 fallback 鏈（設計檔 §7.3.4）：延續「漸進式增強」——缺素材不報錯、不卡住，
/// 沿 <c>pet_visuals.json</c> 登記的 <c>fallback</c> 逐級退回，直到找到有素材的狀態；
/// <c>fallback</c> 為 <c>null</c> 時代表「不換圖、維持目前畫面」（如 CLICK/FEED）。
/// </summary>
/// <remarks>
/// 對照設計檔 §7.3.4 表：
/// <list type="bullet">
///   <item><description><c>SAD</c> / <c>LOW_ENERGY</c> → <c>NEUTRAL</c></description></item>
///   <item><description><c>SLEEP</c> → <c>LOW_ENERGY</c>，若也缺則再退 <c>NEUTRAL</c></description></item>
///   <item><description><c>CLICK</c> / <c>FEED</c> → 不換圖（回傳 <c>null</c>）</description></item>
///   <item><description><c>NEUTRAL</c> 應必有素材（匯入時擋下缺 NEUTRAL 的圖樣，§6.4.3.1）；
///     萬一執行期仍缺，回傳 <c>null</c> 以避免崩潰。</description></item>
/// </list>
/// 本類別不依賴 WPF，可跨平台單元測試。是否「有素材」由呼叫端以委派提供
/// （通常背後是 <see cref="VisualRegistry.ScanUnits"/> 建出的單元池）。
/// </remarks>
public sealed class VisualFallbackResolver
{
    private readonly VisualRegistry _registry;

    public VisualFallbackResolver(VisualRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <summary>
    /// 解析 <paramref name="requested"/> 實際該顯示的狀態：若其本身有素材即回傳它；否則沿 fallback 鏈
    /// 逐級退回到第一個有素材的狀態。整條鏈都無素材、或遇到 <c>null</c> fallback（不換圖）時回傳 <c>null</c>。
    /// </summary>
    /// <param name="requested">原本想顯示的狀態（心情或事件）。</param>
    /// <param name="hasUnits">判斷某狀態是否有可用素材的委派（通常查詢單元池是否非空）。</param>
    /// <returns>應顯示的狀態；<c>null</c> 代表「維持目前畫面／無可用素材」。</returns>
    public PetVisualState? Resolve(PetVisualState requested, Func<PetVisualState, bool> hasUnits)
    {
        ArgumentNullException.ThrowIfNull(hasUnits);

        var visited = new HashSet<PetVisualState>();
        var current = requested;

        while (true)
        {
            if (hasUnits(current))
                return current;

            if (!visited.Add(current))
                return null; // 防呆：設定成環時中止

            var fallback = _registry.GetDefinition(current)?.Fallback;
            if (fallback is not { } next)
                return null; // fallback 為 null（或無定義）→ 不換圖

            current = next;
        }
    }
}
