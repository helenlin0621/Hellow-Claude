namespace DesktopPet.Core.Skins;

/// <summary>
/// Sprite Sheet 依 <c>elapsed</c> 推算「當前該播第幾格」的純數學（設計檔 §7.3.5「格數決定」）。
/// </summary>
/// <remarks>
/// 抽成不依賴 WPF 的純函式，讓最易出錯的推格邏輯（取模／停最後一格）能跨平台單元測試。
/// 公式沿用設計檔：<c>idx = floor(elapsed 秒 × fps)</c>，再依 <c>loop</c> 決定
/// <c>idx % frames</c>（循環）或 <c>min(idx, frames-1)</c>（停在最後一格）。
/// 靜態圖（<c>frames == 1</c>）與缺 <c>fps</c> 的異常單元一律回傳第 0 格（凍結第一格），不丟例外。
/// </remarks>
public static class SpriteSheetFrameMath
{
    /// <summary>
    /// 計算當前格號（0-based）。
    /// </summary>
    /// <param name="elapsed">進入當前單元後經過的時間。</param>
    /// <param name="fps">播放速率；<c>&lt;= 0</c> 視為凍結第一格。</param>
    /// <param name="frames">總格數；<c>&lt;= 1</c> 視為靜態圖（永遠第 0 格）。</param>
    /// <param name="loop"><c>true</c> 循環；<c>false</c> 播完停在最後一格。</param>
    /// <returns>介於 <c>0</c> 至 <c>frames - 1</c> 的格號。</returns>
    public static int FrameIndex(TimeSpan elapsed, int fps, int frames, bool loop)
    {
        if (frames <= 1 || fps <= 0) return 0;

        double seconds = elapsed.TotalSeconds;
        if (seconds <= 0) return 0;

        // 以 long 承接，避免長時間播放（loop）時中間值溢位；取模／夾界後必落在 int 範圍。
        long idx = (long)(seconds * fps);
        if (idx <= 0) return 0;

        return loop
            ? (int)(idx % frames)                       // 循環
            : (int)Math.Min(idx, frames - 1L);          // 播完停在最後一格
    }
}
