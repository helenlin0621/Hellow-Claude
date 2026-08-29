using System.Linq;

namespace DesktopPet.Core.Visuals;

/// <summary>
/// 單元選擇器（設計檔 §7.3.5）：決定「現在該播<b>哪個動畫單元</b>」，<b>不決定播第幾格</b>
/// （後者由 <c>IPetSkinSource</c> 依 <see cref="ElapsedInUnit"/> 推算，B3）。
/// </summary>
/// <remarks>
/// <b>關鍵不變量（§7.3.5）：</b>
/// <list type="number">
///   <item><description><b>抽籤只在「單元切換的時機」發生</b>，不是每次重繪／每次 tick。只有兩種時機重抽：
///     狀態改變，或該狀態設有 <c>rerollIntervalSec</c> 且計時已到。否則回傳當前單元
///     （其內部仍會逐格推進）。每秒重抽會讓多單元狀態看起來像故障。</description></item>
///   <item><description><b>多單元時避免連續抽到同一個</b>：連兩次抽中同一單元視覺上等同沒換。</description></item>
///   <item><description><b><see cref="ElapsedInUnit"/></b> 提供給渲染層計算單元內經過時間；切換單元時重設，
///     這是讓靜態圖與 Sprite Sheet 共存的關鍵（「維持現狀」只凍結單元選擇、不凍結格數推進）。</description></item>
/// </list>
/// 缺素材時走 §7.3.4 fallback 鏈（委由 <see cref="VisualFallbackResolver"/>）：可退回有素材的狀態，
/// 或（如 CLICK/FEED 的 <c>null</c> fallback）「不換圖」——此時<b>不重設時間軸、不改當前單元</b>，
/// 維持目前畫面（事件「進行中不被打斷」由更上層 §7.3.2 判定，非本選擇器職責）。
/// <para>
/// 本類別不依賴 WPF；時鐘與抽籤委派可注入以利確定性單元測試。非執行緒安全（單一寵物 UI 執行緒）。
/// </para>
/// </remarks>
public sealed class PetVisualSelector
{
    private readonly IReadOnlyDictionary<PetVisualState, IReadOnlyList<string>> _pool;
    private readonly VisualFallbackResolver _fallback;
    private readonly Func<DateTime> _now;
    private readonly Func<int, int> _choose; // 給定候選數 n，回傳所選索引 [0, n)

    private bool _hasCurrent;
    private PetVisualState _currentState;
    private string? _currentUnit;
    private DateTime _unitStartTime;
    private DateTime _lastRollTime;

    /// <param name="unitPool">每狀態的單元清單（來自 <see cref="VisualRegistry.ScanUnits"/>）。</param>
    /// <param name="fallbackResolver">缺素材時的 fallback 解析（§7.3.4）。</param>
    /// <param name="clock">時鐘（預設 <see cref="DateTime.UtcNow"/>；用差值計時，避免 DST/校時造成的時間跳動）。</param>
    /// <param name="chooseIndex">抽籤委派：給定候選數回傳索引（預設隨機；測試可注入以確定行為）。</param>
    public PetVisualSelector(
        IReadOnlyDictionary<PetVisualState, IReadOnlyList<string>> unitPool,
        VisualFallbackResolver fallbackResolver,
        Func<DateTime>? clock = null,
        Func<int, int>? chooseIndex = null)
    {
        _pool = unitPool ?? throw new ArgumentNullException(nameof(unitPool));
        _fallback = fallbackResolver ?? throw new ArgumentNullException(nameof(fallbackResolver));
        _now = clock ?? (() => DateTime.UtcNow);

        var rng = new Random();
        _choose = chooseIndex ?? (n => rng.Next(n));

        _unitStartTime = _now();
        _lastRollTime = _unitStartTime;
    }

    /// <summary>當前播放的單元名（尚未成功選過任何單元時為 <c>null</c>）。</summary>
    public string? CurrentUnit => _currentUnit;

    /// <summary>當前被要求的狀態（可能與實際單元所屬狀態不同——缺素材走了 fallback）。</summary>
    public PetVisualState CurrentState => _currentState;

    /// <summary>進入當前單元後經過的時間，供渲染層計算播到第幾格（§7.3.5）。</summary>
    public TimeSpan ElapsedInUnit => _now() - _unitStartTime;

    /// <summary>
    /// 決定當前該播哪個動畫單元。只在「狀態改變」或「<paramref name="rerollIntervalSec"/> 到期」時重抽，
    /// 否則回傳當前單元。缺素材走 fallback；若解析為「不換圖」則維持當前單元、不重設時間軸。
    /// </summary>
    /// <param name="state">被要求的視覺狀態（心情或事件）。</param>
    /// <param name="rerollIntervalSec">此狀態的重抽間隔秒數；<c>0</c> 代表進入後不再重抽（§7.3.5）。</param>
    /// <returns>當前應播的單元名；若從無可用素材可能為 <c>null</c>。</returns>
    public string? ResolveUnit(PetVisualState state, int rerollIntervalSec)
    {
        var now = _now();
        bool stateChanged = !_hasCurrent || state != _currentState;
        bool needReroll = rerollIntervalSec > 0
            && (now - _lastRollTime).TotalSeconds >= rerollIntervalSec;

        if (!stateChanged && !needReroll)
            return _currentUnit; // 維持同一單元；該單元內部仍逐格推進

        var picked = Pick(state);
        if (picked is null)
            return _currentUnit; // 不換圖（null fallback／無素材）：不重設時間軸、不改當前狀態

        _currentState = state;   // 記錄「被要求的狀態」，避免下一 tick 重複 fallback
        _currentUnit = picked;
        _lastRollTime = now;
        _unitStartTime = now;    // 重設動畫時間軸，讓新單元從第 0 格開始
        _hasCurrent = true;
        return _currentUnit;
    }

    /// <summary>
    /// 挑選一個單元：先經 fallback 解析出「有素材」的狀態，再從該狀態的單元清單中挑一個
    /// （單一→直接播；多個→避免連續抽到同一個）。解析為「不換圖」時回 <c>null</c>。
    /// </summary>
    private string? Pick(PetVisualState requested)
    {
        if (_fallback.Resolve(requested, HasUnits) is not { } state)
            return null;

        var list = _pool[state]; // HasUnits 已保證此清單非空
        if (list.Count == 1)
            return list[0];

        // 多單元：排除當前單元以避免連續重複；若排除後無候選（僅一個相異單元）則用全清單。
        var candidates = list.Where(u => u != _currentUnit).ToList();
        if (candidates.Count == 0)
            candidates = list.ToList();

        return candidates[_choose(candidates.Count)];
    }

    private bool HasUnits(PetVisualState state) =>
        _pool.TryGetValue(state, out var list) && list.Count > 0;
}
