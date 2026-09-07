namespace DesktopPet.Core.Interaction;

/// <summary>
/// 雙寵物互動的觸發規則（設計檔 §6.5.4）。純幾何 / 純邏輯，無 WPF 相依，可跨平台單元測試。
/// 由 <c>PetCoordinator</c> 每次互動 tick 呼叫，決定「此刻兩隻該不該互動、互動哪一種」。
/// </summary>
/// <remarks>
/// §6.5.4 觸發條件：
/// <list type="bullet">
///   <item><description><c>greet</c> 打招呼：兩者距離 &lt; 100px 且雙方閒置中。</description></item>
///   <item><description><c>play</c> 一起玩耍：使用者手動觸發，或隨機事件（雙方距離接近時）。</description></item>
///   <item><description><c>cuddle</c> 依偎互動：兩者長時間（&gt; 10 分鐘）維持在接近距離。</description></item>
/// </list>
/// 皆為<b>選配</b>：缺對應素材自動略過（交集判定在 <see cref="PetInteractionChecker"/>，本類別只看幾何與時間）。
/// </remarks>
public static class InteractionRules
{
    /// <summary>打招呼 / 接近距離門檻（§6.5.4：&lt; 100px）。</summary>
    public const double GreetDistancePx = 100.0;

    /// <summary>依偎所需的「長時間維持接近」秒數（§6.5.4：&gt; 10 分鐘）。</summary>
    public const int CuddleSustainedSeconds = 10 * 60;

    /// <summary>互動素材（單張靜態圖，§6.5.2）在畫面上停留的秒數。</summary>
    public static readonly TimeSpan InteractionDisplayDuration = TimeSpan.FromSeconds(2.5);

    /// <summary>兩點的歐氏距離（座標同單位，通常為視窗中心的 DIU）。</summary>
    public static double Distance(double ax, double ay, double bx, double by)
    {
        double dx = ax - bx;
        double dy = ay - by;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    /// <summary>是否在接近距離內（§6.5.4：距離 &lt; <see cref="GreetDistancePx"/>）。</summary>
    public static bool IsClose(double distance) => distance < GreetDistancePx;

    /// <summary>
    /// 依 §6.5.4 決定此刻要觸發的互動類型，回傳 <c>null</c> 表示不觸發。判定：
    /// <list type="number">
    ///   <item><description>不在接近距離內 → <c>null</c>（皆需雙方靠近）。</description></item>
    ///   <item><description>維持接近已超過 <see cref="CuddleSustainedSeconds"/> 且交集含 <c>cuddle</c> → <c>cuddle</c>（深度互動優先）。</description></item>
    ///   <item><description>雙方閒置且交集含 <c>greet</c> → <c>greet</c>。</description></item>
    ///   <item><description>交集含 <c>play</c> → <c>play</c>（接近時的隨機/一起玩耍）。</description></item>
    ///   <item><description>以上皆非 → <c>null</c>。</description></item>
    /// </list>
    /// 交集內容與登記順序由 <see cref="PetInteractionChecker.GetAvailableInteractionTypes"/> 提供，
    /// 本方法只依「素材是否存在」與幾何/時間/閒置條件挑選，不再做張數隨機（§6.5.2）。
    /// </summary>
    /// <param name="availableTypes">雙方共同擁有的互動類型（交集，可能為空）。</param>
    /// <param name="distance">兩視窗中心距離（同單位）。</param>
    /// <param name="sustainedCloseSeconds">目前已「連續維持接近」的秒數。</param>
    /// <param name="bothIdle">兩隻是否都無進行中事件（點擊/餵食/睡眠）。</param>
    public static string? ChooseInteractionType(
        IReadOnlyCollection<string> availableTypes,
        double distance,
        int sustainedCloseSeconds,
        bool bothIdle)
    {
        ArgumentNullException.ThrowIfNull(availableTypes);

        if (availableTypes.Count == 0 || !IsClose(distance))
            return null;

        if (sustainedCloseSeconds > CuddleSustainedSeconds && Contains(availableTypes, "cuddle"))
            return "cuddle";

        if (bothIdle && Contains(availableTypes, "greet"))
            return "greet";

        if (Contains(availableTypes, "play"))
            return "play";

        return null;
    }

    private static bool Contains(IReadOnlyCollection<string> types, string type)
    {
        foreach (var t in types)
        {
            if (string.Equals(t, type, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
