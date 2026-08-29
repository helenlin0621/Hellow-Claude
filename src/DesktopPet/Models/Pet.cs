namespace DesktopPet.Models;

/// <summary>
/// 單一寵物的資料模型（設計檔 §5.1）。純資料容器：
/// 數值變化規則見 §7.4（由 <c>Core/StateManager</c>、<c>Core/HappinessManager</c> 施加），
/// 所有數值一律夾在 0–100（由狀態層負責，模型本身不強制）。
/// 起始數值於寵物建立時（Onboarding）指定，模型不內建遊戲平衡預設值。
/// </summary>
public class Pet
{
    // ── 基本資料 ────────────────────────────────────────────────
    public string Id { get; set; } = string.Empty;        // 寵物 ID
    public string Name { get; set; } = string.Empty;      // 寵物名稱
    public DateTime CreatedDate { get; set; }             // 創建日期
    public int Age { get; set; }                          // 年齡（天數）

    // ── 狀態值（0–100），變化規則見 §7.4 ─────────────────────────
    public int Hunger { get; set; }      // 飢餓度：越高越餓，每 3 分鐘 +1（§7.4.1）
    public int Happiness { get; set; }   // 幸福度：純數值指標，不影響外觀（§7.4.2 / §7.4.3）
    public int Energy { get; set; }      // 能量：越低越累，每 5 分鐘 -1（§7.4.1）
    public int Health { get; set; }      // 健康度：長期指標（§7.4.5）

    // ── 進度資料 ────────────────────────────────────────────────
    public int Level { get; set; }                        // 等級
    public int Experience { get; set; }                   // 經驗值

    /// <summary>
    /// 當前情緒（§7.2.1）。只由 <c>Hunger</c> / <c>Energy</c> 推導，與 <c>Happiness</c> 無關。
    /// 預設為 <see cref="PetMood.Neutral"/>，避免列舉預設值落在 <see cref="PetMood.Sad"/>。
    /// </summary>
    public PetMood CurrentMood { get; set; } = PetMood.Neutral;

    // ── 時間戳記（冷卻判定用；離線期間全部凍結，§7.4.4）─────────────
    public DateTime LastFedTime { get; set; }          // 最後餵食時間（兼作 §7.4.3 餵食冷卻）
    public DateTime LastInteractionTime { get; set; }  // 最後互動時間（§7.4.3 互動冷卻）
    public DateTime LastTickTime { get; set; }         // 上次狀態結算時刻；啟動時重設為現在（§7.4.4）

    // ── 累計秒數（只在程式執行時累加，實現 §7.4.1 的凍結）─────────────
    // 刻意存「累計秒數」而非時間戳記：時間戳會隨真實時間推進，累計值只在程式執行時增加（§7.4.6）。
    public int AwakeIdleSeconds { get; set; }    // 未互動累計，§7.4.2 冷落懲罰判定
    public int HealthCheckSeconds { get; set; }  // 健康度結算計時，§7.4.5 每 30 分鐘

    // ── 自訂圖樣 ────────────────────────────────────────────────
    public string SkinId { get; set; } = string.Empty;          // 目前使用的圖樣 ID
    public string SkinSourceType { get; set; } = string.Empty;  // "builtin" 或 "custom"
    public string SkinFolderPath { get; set; } = string.Empty;  // 圖樣資料夾路徑（內含 anim_*.png，§7.3.3）

    /// <summary>
    /// 該寵物的互動音效組（每隻獨立設定；背景音樂為全域，見 <see cref="GlobalAudioSettings"/>）。
    /// </summary>
    public PetSoundSet Sounds { get; set; } = new();
}

/// <summary>
/// 單一寵物的互動音效組（設計檔 §5.1）。各路徑為 <c>null</c> 時代表使用系統預設音效（§5.2）。
/// </summary>
public class PetSoundSet
{
    public string? ClickSoundPath { get; set; }  // 點擊音效（null = 預設）
    public string? FeedSoundPath { get; set; }   // 進食音效（null = 預設）
    public string? SleepSoundPath { get; set; }  // 睡眠音效（null = 預設）
}
