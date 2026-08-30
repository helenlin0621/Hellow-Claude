namespace DesktopPet.UI;

/// <summary>
/// 右鍵選單指令中「沒有對應視覺事件」的項目（設計檔 §6.3：餵食／玩耍／睡眠／清潔／設置／關於／退出）。
/// </summary>
/// <remarks>
/// 選單共 7 項，但「餵食」「睡眠」在 §7.3.2 有對應的事件圖片（<c>anim_feed_*</c> / <c>anim_sleep_*</c>），
/// 其觸發改走 <see cref="MainWindow.EventTriggered"/>（沿用既有的
/// <see cref="DesktopPet.Core.Visuals.PetVisualState"/>），不在此列舉重複，避免同一次選單點擊
/// 發出兩個語意重疊的事件。本列舉只涵蓋剩下 5 項「純選單指令」。
/// </remarks>
public enum PetMenuAction
{
    /// <summary>玩耍：§7.4.3 與滑鼠點擊共用「+2 幸福度、60 秒冷卻」的回補規則，無專屬視覺事件。</summary>
    Play,

    /// <summary>清潔：設計檔僅列為選單項，數值效果未定義，留待後續任務決定。</summary>
    Clean,

    /// <summary>設置：開啟設定視窗（D5，<c>UI/SettingsWindow.xaml</c>，由 <c>App.xaml.cs</c> 接上）。</summary>
    Settings,

    /// <summary>關於：顯示版本／專案資訊（D5，<c>UI/AboutWindow.xaml</c>）。</summary>
    About,

    /// <summary>退出：由 <see cref="MainWindow"/> 直接處理（呼叫 <c>Application.Shutdown()</c>），見其註解。</summary>
    Exit
}
