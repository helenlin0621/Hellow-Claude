namespace DesktopPet.Models;

/// <summary>
/// 應用程式設定（設計檔 §5.2 / §12.4）。視窗/顯示相關設定與全域音效分開：
/// 音效集中於 <see cref="GlobalAudioSettings"/>（§14）。
/// </summary>
/// <remarks>
/// §5.2 的存檔範例把音效欄位平鋪於 <c>settings</c> 之下；此處依 §14 將全域音效收斂為
/// <see cref="Audio"/> 子物件。目前為規劃階段、尚無既有存檔，最終 JSON 形狀與檔案切分
/// （§8.1 的 <c>settings.json</c>）由 A3 的 <c>StorageManager</c> 決定。
/// </remarks>
public class Settings
{
    public bool AlwaysOnTop { get; set; } = true;    // 始終置頂（§6.1 / §10）
    public bool ClickThrough { get; set; } = false;  // 點穿模式（§2.1 / §10.2）
    public string Theme { get; set; } = "default";   // 佈景主題

    /// <summary>目前語言代碼（如 "zh-TW"）。首次啟動偵測系統語言，不支援時 fallback 至 zh-TW（§12.4）。</summary>
    public string CurrentLanguage { get; set; } = "zh-TW";

    /// <summary>全域背景音樂設定（不分寵物，§5.2）。</summary>
    public GlobalAudioSettings Audio { get; set; } = new();
}

/// <summary>
/// 全域音效設定（設計檔 §5.2 / §14）。背景音樂為全域、不分寵物，避免兩隻寵物同時播放
/// 不同背景音樂互相干擾；每隻寵物的互動音效見 <see cref="PetSoundSet"/>。
/// </summary>
public class GlobalAudioSettings
{
    public int Volume { get; set; } = 80;    // 音量 0–100

    /// <summary>背景音樂曲目路徑（最多 3 首）；啟動/切換時隨機挑一首播放（§5.2）。</summary>
    public List<string> BackgroundMusicPaths { get; set; } = new();

    public bool IsMuted { get; set; } = false;  // 背景音樂靜音開關（§5.2）
}
