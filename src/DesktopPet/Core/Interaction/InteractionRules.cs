namespace DesktopPet.Core.Interaction;

/// <summary>
/// 雙寵物互動的距離與觸發條件（設計檔 §6.5.4）：純幾何／計時判斷，不依賴 WPF 或任何寵物模型，
/// 呼叫端（<c>Core/PetCoordinator.cs</c>，E3）負責換算座標與持有累計秒數。
/// </summary>
/// <remarks>
/// <b>§6.5.4 對照表：</b>
/// <list type="bullet">
///   <item><description><c>greet</c> 打招呼：兩者距離 &lt; 100px <b>且</b>雙方閒置中——見
///     <see cref="ShouldGreet"/>。</description></item>
///   <item><description><c>cuddle</c> 依偎互動：兩者長時間（&gt; 10 分鐘）維持在接近距離——見
///     <see cref="ShouldCuddle"/>／<see cref="TickCloseSeconds"/>。</description></item>
///   <item><description><c>play</c> 一起玩耍：使用者手動觸發，<b>或</b>隨機事件（雙方距離接近時）。
///     設計檔只定義了「手動觸發」（無距離門檻）與「接近時可能隨機發生」，<b>未定義隨機事件的
///     機率／檢查間隔</b>。本類別只提供「接近」的判斷（<see cref="IsClose"/>）供手動觸發時檢查
///     素材/距離是否允許；自動隨機觸發那部分刻意不在本類別（也不在 <c>PetCoordinator</c>）實作，
///     避免無中生有一個設計檔沒給的機率數字（見 <c>PetCoordinator</c> 類別註解）。</description></item>
/// </list>
/// <b>「接近距離」沿用 greet 的 100px：</b>設計檔只在 greet 給出具體數字，cuddle／play 只寫「接近
/// 距離」而未另訂數值，故三者共用同一個 <see cref="CloseDistancePx"/> 門檻，避免另外發明一個數字。
/// <para>純邏輯，無狀態（除呼叫端自行持有的累計秒數外），可安全共用單一呼叫方式；可跨平台單元測試。</para>
/// </remarks>
public static class InteractionRules
{
    /// <summary>「接近」的距離門檻（像素，與呼叫端傳入座標同單位，通常是 DIU）。嚴格小於才算接近。</summary>
    public const double CloseDistancePx = 100;

    /// <summary>依偎互動所需的最短持續接近秒數（§6.5.4：「長時間，例如 &gt; 10 分鐘」）。</summary>
    public const int CuddleSustainSeconds = 10 * 60;

    /// <summary>兩點的歐幾里得距離（呼叫端負責換算成同一單位，通常是兩隻寵物視窗中心點的 DIU 座標）。</summary>
    public static double Distance(double xA, double yA, double xB, double yB)
    {
        double dx = xA - xB;
        double dy = yA - yB;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>是否處於「接近」距離（<see cref="CloseDistancePx"/>，嚴格小於）。</summary>
    public static bool IsClose(double distance) => distance < CloseDistancePx;

    /// <summary>greet 觸發條件：接近 <b>且</b> 雙方閒置中（§6.5.4）。「閒置」的定義由呼叫端決定並傳入。</summary>
    public static bool ShouldGreet(double distance, bool petAIdle, bool petBIdle) =>
        IsClose(distance) && petAIdle && petBIdle;

    /// <summary>
    /// 1 Hz tick 用：累加「持續接近」秒數，未接近時歸零——呼應
    /// <see cref="DesktopPet.Models.Pet.AwakeIdleSeconds"/> 的累計秒數模式（§7.4.2：只在執行期
    /// 累加、離開條件即歸零）。呼叫端（<c>PetCoordinator</c>）持有累計值，每秒呼叫一次並存回。
    /// </summary>
    public static int TickCloseSeconds(int currentSeconds, bool isClose) =>
        isClose ? currentSeconds + 1 : 0;

    /// <summary>cuddle 觸發條件：持續接近秒數超過 <see cref="CuddleSustainSeconds"/>（嚴格大於，同 §7.4.2 冷落門檻慣例）。</summary>
    public static bool ShouldCuddle(int closeSeconds) => closeSeconds > CuddleSustainSeconds;
}
