# Windows 桌面寵物系統 - 設計檔

**文檔版本**: 2.0  
**最後更新**: 2026-08-29  
**項目狀態**: 規劃階段  
**開發者**: [你的名字]

---

## 📋 1. 項目概述

### 1.1 項目目標
開發一個功能豐富的 Windows 桌面寵物應用，提供用戶在桌面上飼養虛擬寵物的體驗。

### 1.2 目標平台
- **OS**: **Windows 10 (1809) 及以上**
- **架構**: x64 / Arm64
- **設計理念**: 低占用、可交互、美觀


### 1.3 開發優先級
- **Phase 1 (MVP)**: 基礎寵物顯示、動畫、交互
- **Phase 2**: 狀態系統、存檔機制
- **Phase 3**: 高級功能（遊戲、進化等）

---

## 🎯 2. 功能需求清單

### 2.1 核心功能 (必須)
- [ ] 窗口管理
  - 透明背景窗口
  - 始終置頂選項
  - 點穿模式（允許點擊下方）
  
- [ ] 寵物動畫系統（詳見第 7.3 節）
  - 心情圖片 3 類：`SAD` / `LOW_ENERGY` / `NEUTRAL`
  - 事件圖片 3 類：點擊 / 進食 / 睡眠
  - **每一類皆支援 1 至多個「動畫單元」，多個時由系統隨機挑選播放**
  - **單元可為單張靜態圖，也可為 Sprite Sheet 連續動畫，兩者可自由混用**（6.4.5 節）
  - 移動動畫
  - 過渡效果
  
- [ ] 交互系統
  - 滑鼠點擊反應
  - 拖曳功能
  - 右鍵菜單
  - 雙擊特殊動作
  
- [ ] 寵物狀態
  - 飢餓度 (0-100)
  - 幸福度 (0-100)
  - 能量 (0-100)
  - 健康度 (0-100)

- [ ] **多寵物支援**（詳見第 6.5 節）
  - 使用者可自選養 1 隻或 2 隻
  - 每隻寵物獨立視窗、獨立狀態、獨立圖樣
  - 有互動素材時觸發互動行為，無則各自獨立行動

### 2.2 中級功能 (推薦)
- [ ] 狀態面板 (UI 顯示)
- [ ] 進度保存 / 加載
- [ ] 系統托盤集成
- [ ] 音效系統
  - [ ] **自訂音效上傳**（詳見第 6.6 節）：點擊、進食、睡眠、背景音樂皆可替換
- [ ] 簡單菜單（餵食、睡眠、清潔等）
- [ ] 飼養天數統計
- [ ] **自訂寵物圖樣**（詳見第 6.4 節）
  - [ ] 匯入本地圖片檔
  - [ ] 內建官方主題庫
- [ ] **統一素材管理中心 UI**（詳見第 6.6 節，圖片+音效整合介面）

### 2.3 高級功能 (可選)
- [ ] 多主題/皮膚系統
- [ ] 小遊戲模塊
- [ ] 成就系統
- [ ] 進化 / 升級機制
- [ ] 換裝系統
- [ ] 天氣連動（7.5 節，預設關閉）
- [ ] 多語言支持
- [ ] 社交分享功能

---

## 🏗️ 3. 系統架構

### 3.1 整體架構圖
```
┌─────────────────────────────────────┐
│        Main Application             │
│      (App Lifecycle Manager)        │
└────────┬────────────────────────────┘
         │
  ┌──────▼──────────┐
  │ Pet Coordinator  │  ← 管理 1-2 隻寵物實例
  │ (多寵物協調層)     │     + 處理跨寵物互動邏輯
  └──────┬───────────┘
         │
    ┌────┴─────────────────┐
    │                       │
┌───▼────────┐      ┌───────▼────┐
│ Pet Instance│      │Pet Instance│  ← 每隻各自一份
│    #1       │      │    #2      │     (可為空)
└───┬─────────┘      └───┬────────┘
    │                    │
┌───┴──────┬──────┬──────┴───┐
│          │      │          │
┌▼──┐  ┌───▼─┐ ┌──▼───┐ ┌───▼──┐
│Pet │  │State│ │Render│ │Input │
│Logic│ │Mgr  │ │Engine│ │Mgr   │
└────┘  └─────┘ └──────┘ └──────┘
    │          │      │          │
    └───┬──────┴──────┴──────────┘
        │
  ┌─────▼──────────┐
  │ Storage / JSON │
  │  (Persistence) │
  └────────────────┘
```

**架構要點**：
- 每隻寵物是獨立的「Pet Instance」，各自擁有獨立視窗、狀態、圖樣、動畫循環
- `Pet Coordinator` 負責統籌：偵測兩隻寵物是否有共用的互動素材，若有則觸發互動行為，若無則各自獨立運作（詳見 6.5 節）
- 單寵物模式時，Pet Instance #2 直接不建立，Coordinator 邏輯自動略過互動檢查

### 3.2 主要模塊說明

| 模塊 | 職責 | 技術 |
|------|------|------|
| **Pet Coordinator** | 管理多寵物生命週期、跨寵物互動判定 | C# 類別 |
| **Pet Instance** | 單一寵物的完整運行單元 | C# 類別 |
| **Pet Logic** | 寵物狀態計算、行為邏輯 | C# 類別 |
| **State Manager** | 狀態數據管理、更新 | 數據模型 |
| **Render Engine** | 圖形繪製、格數推進（7.1.1 動態頻率） | WPF |
| **Input Manager** | 滑鼠/鍵盤事件處理 | WPF |
| **Storage** | JSON 持久化 | System.IO |

---

## 💻 4. 技術棧選擇

**選定：C# WPF + .NET 8**

**專案設定**：
```xml
<TargetFramework>net8.0-windows</TargetFramework>
<UseWPF>true</UseWPF>
<PublishSingleFile>true</PublishSingleFile>
<SelfContained>true</SelfContained>
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
```

**依賴庫**：
- `System.Text.Json` — JSON 序列化（.NET 8 內建）
- `System.Drawing.Common` — 圖形操作
- 可選：`NAudio` — 音效播放

**已知限制**（實作時必須知道，否則會踩坑）：

| 限制 | 影響 | 對策 |
|------|------|------|
| `AllowsTransparency=True` 使該視窗走軟體算圖，且無法與 D3D / WindowsFormsHost 互通 | 高頻全視窗重繪會成為瓶頸 | 7.1.1 節動態渲染頻率：單格單元暫停重繪，多格才升至 12–15 fps |
| WPF 無點穿內建 API | 2.1 節點穿模式 | P/Invoke 設定 `WS_EX_TRANSPARENT`（10.2 節） |
| `System.Text.Json` 預設將列舉序列化為數字 | 5.2 節存檔用字串（`"NEUTRAL"`），對應寫錯會導致舊存檔讀不回來 | 註冊 `JsonStringEnumConverter` + 自訂命名策略，使 `PetMood.LowEnergy` → `"LOW_ENERGY"` |
| WPF 為 Windows 專屬 | 無 macOS / Linux 路徑 | 已接受。跨平台屬重寫而非移植 |

> 曾評估 WinForms、Electron、Avalonia、Tauri、Godot、JavaFX，評估過程見 git 歷史。

## 📊 5. 數據結構設計

### 5.1 寵物數據模型
```csharp
public class Pet
{
    public string Id { get; set; }              // 寵物 ID
    public string Name { get; set; }            // 寵物名稱
    public DateTime CreatedDate { get; set; }   // 創建日期
    public int Age { get; set; }                // 年齡（天數）
    
    // 狀態值 (0-100)，變化規則見 7.4 節
    public int Hunger { get; set; }             // 飢餓度：越高越餓，每3分鐘 +1
    public int Happiness { get; set; }          // 幸福度：純數值指標，不影響外觀（7.4.2 / 7.4.3）
    public int Energy { get; set; }             // 能量：越低越累，每5分鐘 -1
    public int Health { get; set; }             // 健康度：長期指標，見 7.4.5
    
    // 進度數據
    public int Level { get; set; }              // 等級
    public int Experience { get; set; }         // 經驗值
    public PetMood CurrentMood { get; set; }    // 當前情緒（3 值列舉，見 7.2.1）
    public DateTime LastFedTime { get; set; }   // 最後餵食時間（兼作 7.4.3 餵食冷卻判定）
    public DateTime LastInteractionTime { get; set; }  // 最後互動時間（兼作 7.4.3 互動冷卻 + 7.4.2 冷落懲罰判定）
    public DateTime LastTickTime { get; set; }  // 上次狀態結算時刻；離線期間全部凍結（7.4.4），啟動時重設為現在時刻

    // 累計秒數（只在程式執行時累加，實現 7.4.1 的凍結）
    public int AwakeIdleSeconds { get; set; }   // 未互動累計，7.4.2 冷落懲罰判定
    public int HealthCheckSeconds { get; set; } // 健康度結算計時，7.4.5 每 30 分鐘
    
    // 自訂圖樣相關
    public string SkinId { get; set; }          // 目前使用的圖樣ID
    public string SkinSourceType { get; set; }  // "builtin" 或 "custom"
    public string SkinFolderPath { get; set; }  // 圖樣「資料夾」路徑（內含 anim_*.png，見 7.3.3）
    
    // 自訂音效相關（每隻寵物獨立設定，背景音樂除外）
    public PetSoundSet Sounds { get; set; }     // 該寵物的互動音效組
}

public class PetSoundSet
{
    public string ClickSoundPath { get; set; }  // 點擊音效（null = 使用預設）
    public string FeedSoundPath { get; set; }   // 進食音效
    public string SleepSoundPath { get; set; }  // 睡眠音效
}

public class SkinInfo
{
    public string Id { get; set; }              // 圖樣唯一ID
    public string Name { get; set; }            // 顯示名稱
    public string SourceType { get; set; }      // "builtin" / "custom"
    public string FolderPath { get; set; }      // 圖樣資料夾路徑
    public DateTime ImportedDate { get; set; }  // 匯入日期（custom用）
}

// 心情列舉，見 7.2.1
public enum PetMood
{
    Sad,        // 飢餓：Hunger > 70
    LowEnergy,  // 低能量：Energy < 20
    Neutral     // 一般：以上皆非
}

public class GameState
{
    public List<Pet> Pets { get; set; }         // 目前飼養的寵物清單（1-2隻）
    public int MaxPetSlots { get; set; } = 2;   // 上限（固定為2，未來可調整）
    public Dictionary<string, int> Achievements { get; set; }
    public Dictionary<string, object> Settings { get; set; }
}
```

> **變更說明**：原本的 `CurrentPet` (單一寵物) 改為 `Pets` (清單)，長度為 1 或 2，依使用者選擇而定。所有存取單一寵物邏輯的地方需改為遍歷清單處理。

### 5.2 存檔格式 (JSON)
```json
{
  "pets": [
    {
      "id": "pet_001",
      "name": "Fluffy",
      "createdDate": "2026-08-27T00:00:00",
      "age": 0,
      "hunger": 50,
      "happiness": 80,
      "energy": 70,
      "health": 100,
      "level": 1,
      "experience": 0,
      "currentMood": "NEUTRAL",
      "lastFedTime": "2026-08-27T12:00:00",
      "lastInteractionTime": "2026-08-27T12:00:00",
      "lastTickTime": "2026-08-27T12:00:00",
      "awakeIdleSeconds": 0,
      "healthCheckSeconds": 0,
      "skinId": "builtin_cat",
      "skinSourceType": "builtin",
      "skinFolderPath": "assets/themes/builtin_cat/",
      "sounds": {
        "clickSoundPath": null,
        "feedSoundPath": "custom_sounds/pet_001_feed.mp3",
        "sleepSoundPath": null
      }
    },
    {
      "id": "pet_002",
      "name": "Mochi",
      "createdDate": "2026-08-27T00:00:00",
      "age": 0,
      "hunger": 60,
      "happiness": 90,
      "energy": 80,
      "health": 100,
      "level": 1,
      "experience": 0,
      "currentMood": "NEUTRAL",
      "lastFedTime": "2026-08-27T12:00:00",
      "lastInteractionTime": "2026-08-27T12:00:00",
      "lastTickTime": "2026-08-27T12:00:00",
      "awakeIdleSeconds": 0,
      "healthCheckSeconds": 0,
      "skinId": "custom_001",
      "skinSourceType": "custom",
      "skinFolderPath": "custom_skins/custom_001/",
      "sounds": {
        "clickSoundPath": null,
        "feedSoundPath": null,
        "sleepSoundPath": null
      }
    }
  ],
  "settings": {
    "volume": 80,
    "alwaysOnTop": true,
    "clickThrough": false,
    "theme": "default",
    "backgroundMusicPaths": [
      "custom_sounds/bgm_lofi.mp3",
      "custom_sounds/bgm_piano.mp3"
    ],
    "isMuted": false
  }
}
```

> **設計說明**：互動音效（點擊/進食/睡眠）為**每隻寵物獨立設定**，`null` 代表使用系統預設音效；背景音樂則為**全域設定**（不分寵物），最多可設定 3 首曲目，系統每次啟動/切換時隨機挑選一首播放，並提供獨立的靜音開關（`isMuted`），避免兩隻寵物同時播放不同背景音樂互相干擾。

> **注意**：`pets` 陣列長度為 1 時即為單寵物模式；長度為 2 時互動邏輯才會啟動（詳見 6.5 節）。

---

## 🎨 6. UI/UX 設計規範

### 6.1 窗口配置
- **初始大小**: 320x240 像素
- **背景**: 透明 PNG (1:1 比例寵物)
- **始終可見**: 系統托盤最小化按鈕
- **多寵物模式**: 每隻寵物各自一個獨立透明窗口實例，可分別拖曳到桌面不同位置（詳見 6.5 節）

### 6.2 狀態面板 (可摺疊)
```
┌─────────────────────┐
│ 🐱 Fluffy | ☰ Menu │
├─────────────────────┤
│ ❤️ Health: ███░░░ 70%│
│ 😊 Mood:   ████░░ 80%│
│ 🍽️ Hunger: ██░░░░ 20%│
│ ⚡ Energy: ███░░░░ 60%│
├─────────────────────┤
│ Days: 5 | Lvl: 1    │
└─────────────────────┘
```

**顯示說明**：
- `😊 Mood` 顯示的是 `Happiness` 幸福度。其數值變化規則見 7.4.2 / 7.4.3 節
- 幸福度**不影響寵物外觀**——7.2.1 節的心情分支只看 `Hunger` 與 `Energy`。面板上幸福度 90% 但寵物顯示 `NEUTRAL` 圖片是正常的
- `❤️ Health` 健康度為長期指標，變化遠慢於其他三項（見 7.4.5 節）

### 6.3 右鍵菜單選項
- 餵食
- 玩耍
- 睡眠
- 清潔
- 設置
- 關於
- 退出

### 6.4 自訂寵物圖樣系統

**設計決策** (2026-08-27 確認):
- 素材類型: **靜態圖片與 Sprite Sheet 並存**，統一為「動畫單元」模型（見 6.4.5 節）
- 素材來源: **雙軌並行**
  1. 使用者匯入本地圖片檔
  2. 內建 **2 套**官方主題供選擇（開發者比照使用者規格製作，無特殊待遇）
- **不提供裁切/預覽編輯工具**，改以嚴格的格式/大小驗證 + 明確錯誤訊息把關（見 6.4.3.1 節）
- **一套圖樣 = 一個資料夾，而非單一檔案**：每套圖樣涵蓋 7.3 節定義的 6 種圖片類型（3 種心情 + 3 種事件），**每一類型皆支援 1 至多個單元**，多個時由系統隨機挑選播放。檔名規則與播放邏輯見 7.3 節


#### 6.4.1 內建官方主題
- 隨程式內建 **2 套**預設主題（例如：貓、狗）
- **設計原則**：內建主題由開發者製作，但必須遵循與使用者匯入**完全相同的格式規格**（見 6.4.3 節），不享有特殊待遇。好處是驗證邏輯、渲染邏輯可共用同一套程式碼，不用為內建主題另外寫例外處理
- 存放於 `Resources/Assets/Themes/` 目錄下，隨程式打包發佈
- 使用者可在設置面板中直接切換

#### 6.4.2 使用者自訂匯入
- **採「分類型逐格匯入」**：素材管理面板為 7.3 節的 6 種圖片類型各提供一個匯入區塊，使用者可只匯入必填的 `NEUTRAL`（`anim_idle_*`）即開始使用，其餘類型可日後陸續補齊（缺圖時走 7.3.4 節的 fallback 鏈）
- 每個類型區塊可**重複匯入多個單元**，系統自動遞增序號存檔（`anim_idle_1.png`、`anim_idle_2.png`…），使用者不需自行命名
- 另提供「匯入整個資料夾」的進階選項：使用者若已依 7.3.3 節命名規則備妥檔案，可一次匯入整套
- 支援格式: `.png`（建議，支援透明背景）、`.jpg`
- 匯入後複製一份到使用者資料目錄的**該圖樣專屬資料夾**內（`custom_skins/{skinId}/`），避免原始檔案被移動/刪除後失效
- **不提供裁切/預覽編輯工具**（確認決策：使用者需自行準備好符合規格的圖片），改為在匯入時嚴格驗證格式與大小，並清楚告知失敗原因（見 6.4.3.1 節）

##### 6.4.2.1 匯入類型分支

匯入時多一個選擇，決定該檔案是單格還是多格：

```
┌── 匯入素材 ─────────────────────────┐
│ 類型: NEUTRAL (anim_idle_*)          │
│ 檔案: [選擇檔案...] cat_walk.png     │
│                                      │
│ 素材形式:                             │
│   ◉ 單張圖片                          │
│   ○ 連續動畫 (Sprite Sheet)          │
│     ├ 格數:     [ 6 ]                │
│     ├ 每格寬度: [ 256 ] px           │
│     ├ 播放速率: [ 12 ] fps           │
│     └ ☑ 循環播放                     │
│                                      │
│           [取消]  [匯入]              │
└──────────────────────────────────────┘
```

**設計要點**：
- **預設選「單張圖片」**，維持 6.4.2 節的低門檻——只想丟一張 PNG 的使用者完全不會碰到多出來的欄位
- 選「連續動畫」才展開格數/寬度/fps 欄位，並套用 6.4.3.2 節的額外驗證
- 每格高度由圖片總高自動推得（Sprite Sheet 一律**單列橫向排列**，不支援多列網格，避免驗證與切圖邏輯複雜化）
- **使用者永遠不需要手寫 `skin.json`**（6.4.5 節），該檔由匯入流程自動產生與維護

> ⚠️ **不以圖片尺寸推導格式**。看到 2048×256 就猜是 8 格 Sprite Sheet 的作法很脆弱：使用者匯入一張 512×256 的橫幅角色圖會被誤判為 2 格，畫面只顯示左半邊，且系統無從產生錯誤訊息。格數必須由使用者明確指定。

#### 6.4.3 圖片規格建議

**單張圖片**（`frames: 1`）：

| 項目 | 建議值 | 說明 |
|------|--------|------|
| 尺寸 | 256x256 px（正方形） | 建議值，非強制；比例差異過大會由系統自動置中裁切或留白，非使用者手動調整 |
| 格式 | PNG（建議，支援透明背景）、JPG | 僅接受這兩種，其餘一律拒絕 |
| 檔案大小 | < 2MB | 避免載入過慢 |

**Sprite Sheet**（`frames > 1`）：

| 項目 | 建議值 | 說明 |
|------|--------|------|
| 排列方式 | **單列橫向** | 不支援多列網格，避免切圖與驗證邏輯複雜化 |
| 每格尺寸 | 256x256 px | 同單張圖片建議值 |
| 格數 | 2 ~ 16 格 | 上限為建議值，超過時警告但不阻擋 |
| 總寬度 | 格寬 × 格數（需整除） | 阻擋型驗證，見 6.4.3.2 節 |
| 格式 | PNG（強烈建議） | JPG 有壓縮雜訊，格與格交界處易出現色塊溢出 |
| 檔案大小 | < 8MB | 較單張放寬，因一個檔案包含多格 |
| 播放速率 | 12 ~ 15 fps | 桌寵動畫此區間已足夠，且更有手繪感；不建議 30 fps（見 10.1 節） |

##### 6.4.3.1 匯入驗證與錯誤回饋
由於不提供裁切/預覽工具，匯入時的驗證與錯誤訊息就是使用者唯一的把關機制，必須清楚明確：

| 驗證項目 | 判定規則 | 失敗時顯示的錯誤訊息（範例） |
|---------|---------|---------------------------|
| 副檔名 | 僅接受 `.png` / `.jpg` / `.jpeg` | 「不支援此檔案格式，請使用 PNG 或 JPG 圖片」 |
| 檔案大小 | 單張 < 2MB；Sprite Sheet < 8MB | 「圖片檔案過大（目前 X MB），請壓縮至 Y MB 以內」 |
| 圖片可讀性 | 檔案需為有效圖片（非損毀檔案偽裝副檔名） | 「圖片檔案已損毀或無法讀取，請重新選擇」 |
| 尺寸比例（僅警告，不阻擋） | 長寬比例與 1:1 差異過大 | 「圖片非正方形，可能會有裁切或變形，是否仍要使用？」（提供繼續/取消） |

**驗證流程**：匯入 → 逐項檢查 → 任一「阻擋型」規則不通過就中止匯入並顯示對應錯誤，不寫入任何設定；僅「警告型」規則（如比例）則詢問使用者是否繼續。

##### 6.4.3.2 Sprite Sheet 額外驗證

僅在 6.4.2.1 節選擇「連續動畫」時套用，**在 6.4.3.1 的通用驗證之後執行**：

| 驗證項目 | 判定規則 | 類型 | 錯誤訊息（範例） |
|---------|---------|------|----------------|
| 總寬整除 | `圖片寬度 % 格數 == 0` | 阻擋 | 「圖片寬度 2000px 無法被 6 格整除。請確認格數，或將寬度調整為 6 的倍數」 |
| 格寬一致 | `圖片寬度 / 格數 == 使用者填的格寬` | 阻擋 | 「依圖片寬度推算每格為 333px，與填寫的 256px 不符。請確認格數與格寬」 |
| 格數下限 | `格數 >= 2` | 阻擋 | 「連續動畫至少需 2 格。若只有一格，請改選『單張圖片』」 |
| 格數上限 | `格數 <= 16` | 警告 | 「格數較多（X 格），檔案載入與記憶體用量會增加，是否仍要使用？」 |
| fps 範圍 | `1 <= fps <= 30` | 阻擋 | 「播放速率需介於 1 至 30 fps 之間」 |
| 單格比例 | 每格長寬比與 1:1 差異過大 | 警告 | 「每格尺寸為 W×H，非正方形，可能會有裁切或變形，是否仍要使用？」 |

> **整除驗證是最重要的一條**。這是 Sprite Sheet 最常見的製作錯誤——匯出時多一兩個像素的邊界，切圖就會整組錯位，而且錯位是漸進的（第 1 格正常、第 6 格明顯偏移），使用者很難自行察覺原因。因此設為阻擋型並在訊息中直接算出正確數值。

#### 6.4.4 素材抽象介面
目前僅實作靜態圖片顯示，但渲染層需設計為「圖層抽象介面」，方便未來替換為 Sprite Sheet 動畫來源而不需重寫上層邏輯。

**介面回傳型別改為 frame descriptor**，讓靜態圖與 Sprite Sheet 共用同一條路徑：

```csharp
// 一格畫面的描述：哪張圖的哪個矩形
public readonly struct FrameRef
{
    public BitmapSource Source { get; init; }   // 底圖（整張 PNG，含 Sprite Sheet 全圖）
    public Int32Rect    Rect   { get; init; }   // 要顯示的區域
}

public interface IPetSkinSource
{
    // elapsed = 進入當前單元後經過的時間，用於決定播到第幾格
    FrameRef GetFrame(PetVisualState state, TimeSpan elapsed);

    // 供 6.6.1 面板顯示完成度用
    IReadOnlyList<VisualUnitInfo> GetUnits(PetVisualState state);
}
```

**為何不沿用 `BitmapImage`**：Sprite Sheet 每格若都 crop 成新的 `BitmapImage`，播放時每秒會產生 12–15 個短命物件，GC 壓力大。改回傳「底圖 + 矩形」後，底圖只載入一次，切格只是換矩形座標，WPF 端用 `CroppedBitmap` 或直接在 `ImageBrush` 上設 `Viewbox` 即可，不需重新配置記憶體。

**靜態圖的實作**：`Rect` 填整張圖的完整範圍，忽略 `elapsed` 參數。也就是說靜態圖不是特例分支，而是**只有一格的動畫**——這正是 6.4.5 節統一模型的核心。

上層渲染邏輯只呼叫 `GetFrame(state, elapsed)`，不需要知道底層是單格靜態圖還是多格 Sprite Sheet。

#### 6.4.5 統一動畫單元模型

**核心原則：靜態圖 = `frames` 為 1 的 Sprite Sheet。**

若把兩者當成兩種格式並存，`if (isSpriteSheet) ... else ...` 會散落在計時器、選圖、快取、匯入驗證各處。統一成「所有素材都是**動畫單元**，只是格數不同」後，全系統只有一套邏輯：

| 概念 | 靜態圖 | Sprite Sheet |
|------|--------|-------------|
| 格數 | 1 | N |
| 播放行為 | 播完停在第 1 格 | 播完停住或循環 |
| 7.3.5 隨機挑選的單位 | 一個單元 | 一個單元 |
| 7.3.4 fallback 鏈 | 完全相同 | 完全相同 |
| 6.4.3.1 通用驗證 | 完全相同 | 完全相同 |

7.3.5 節的隨機挑選本來就是在挑「一個播放單元」，它**不需要知道那個單元裡有幾格**。

##### 允許混用的三個層級

| 層級 | 說明 | 範例 |
|------|------|------|
| 不同圖樣不同形式 | 每套圖樣各自決定 | 內建貓用 Sprite Sheet、使用者匯入的是靜態圖 |
| 同一圖樣不同狀態 | 每個狀態各自決定 | `idle` 用 8 格動畫，`sad` 只有一張靜態圖 |
| **同一狀態內混用** | 同一狀態的多個單元可不同形式 | `anim_idle_1` 是靜態圖、`anim_idle_2` 是 6 格動畫，隨機抽到哪個播哪個 |

第三層看似最激進，但在統一模型下是**免費的**——選圖器抽中一個單元後交給渲染層，全程不需要知道格數。

##### `skin.json` 素材描述檔

每套圖樣資料夾內含一份 `skin.json`，記錄各單元的格數資訊：

```json
{
  "schemaVersion": 2,
  "units": {
    "anim_idle_1":  { "frames": 1 },
    "anim_idle_2":  { "frames": 6, "frameWidth": 256, "fps": 12, "loop": true },
    "anim_idle_3":  { "frames": 1 },
    "anim_sad_1":   { "frames": 1 },
    "anim_tired_1": { "frames": 4, "frameWidth": 256, "fps": 8,  "loop": true },
    "anim_tired_2": { "frames": 1 },
    "anim_click_1": { "frames": 8, "frameWidth": 256, "fps": 15, "loop": false },
    "anim_feed_1":  { "frames": 1 },
    "anim_sleep_1": { "frames": 2, "frameWidth": 256, "fps": 2,  "loop": true }
  }
}
```

| 欄位 | 說明 |
|------|------|
| `schemaVersion` | 格式版本，目前為 `2` |
| `frames` | 格數。`1` 即為靜態圖，此時其餘欄位可省略 |
| `frameWidth` | 每格寬度（px）。每格高度由圖片總高推得 |
| `fps` | 播放速率 |
| `loop` | 播完是否循環。`false` 時停在最後一格 |

**兩條關鍵規則**：

1. **使用者永遠不需要手寫此檔**。由 6.4.2.1 節的匯入流程自動產生與維護。
2. **缺少 `skin.json` 的資料夾，一律視為所有單元皆 `frames: 1`**。這讓既有素材與手動整理的資料夾**零遷移**即可繼續使用，也保住 7.3.3 節「丟進資料夾就生效」的低門檻設計。


---

### 6.5 多寵物系統

**設計決策** (2026-08-27 確認):
- 使用者可自選飼養 **1 隻或 2 隻** 寵物
- 每隻寵物是獨立視窗、獨立狀態、獨立圖樣
- **互動行為採「漸進式增強」策略**：有對應互動素材時觸發互動，沒有則各自獨立行動 —— 不會因為缺素材而報錯或卡住

#### 6.5.1 選擇飼養數量的時機
- **首次啟動**：引導畫面詢問「養 1 隻還是 2 隻？」
- **設置面板**：隨時可新增第 2 隻，或送走其中一隻（需二次確認，避免誤觸失去進度）

#### 6.5.2 互動素材規格定義

**設計決策** (2026-08-27 確認):
- 初始定義 **3 種**互動類型（上限3種，未來可由開發者擴充新類型，不需改動核心程式碼）
- 觸發互動的最低門檻：**雙方寵物只要有任一相同類型的素材**即可觸發該類型互動；完全沒有共同類型則兩隻各自獨立行動
- **混搭圖樣允許**：一隻用自訂圖樣、一隻用內建主題完全沒問題，但若要觸發互動，雙方仍必須各自備有至少一種相符的互動素材類型（混搭不影響判定邏輯，只看素材是否存在）

**互動類型**：`greet` / `play` / `cuddle` 三種**暫定採用**，符合預期。**不排除未來新增**——擴充方式見本節末的 `interaction_types.json`，新增類型不需改動核心程式碼。

| 類型代號 | 中文名稱 | 觸發情境 | 檔名 |
|---------|---------|---------|------|
| `greet` | 打招呼 | 兩隻寵物靠近時的基本反應 | `interaction_greet.png` |
| `play` | 一起玩耍 | 使用者手動觸發或隨機事件 | `interaction_play.png` |
| `cuddle` | 依偎互動 | 兩隻長時間待在一起 | `interaction_cuddle.png` |

**檔名規則**：
- 統一命名為 `interaction_[類型代號].png`，與該寵物的圖樣放在同一個素材資料夾內
- 素材格式延續 6.4.3 節的靜態圖片規格（PNG/JPG、< 2MB），**不需要額外規格**，方便使用者理解與製作
- 每種類型**固定為單張靜態圖**

> **互動素材刻意不比照 7.3 節開放多張隨機播放。**
>
> 原因：互動是**兩隻寵物同時發生**的行為，雙方各自隨機抽圖會出現組合不協調的情況（例如 A 抽到「熱情揮手」、B 抽到「冷淡點頭」），畫面會顯得不同步。要做對就得引入「配對表」或「主從決定」機制，複雜度遠高於單一寵物的隨機播放。
>
> 因此 `interaction_*.png` 維持一種類型一張，由 6.5.3 節的交集判定決定播哪一種，不再做張數上的隨機。若日後要放寬，須先解決雙方抽圖的同步問題。

**開發者未來擴充新類型的方式**：
新增互動類型不需改程式碼，只要在設定檔 `interaction_types.json` 登記新代號即可：
```json
{
  "types": ["greet", "play", "cuddle", "future_type_here"]
}
```
系統讀取此清單決定要檢查哪些互動素材，UI 素材管理面板也會依此清單動態產生對應的上傳欄位。

#### 6.5.3 互動素材檢測機制

```csharp
public class PetInteractionChecker
{
    // 回傳兩隻寵物「共同擁有」的互動類型清單（可能為空、1種、2種或3種）
    public List<string> GetAvailableInteractionTypes(Pet petA, Pet petB)
    {
        var typesA = GetInteractionAssetTypes(petA);  // 例如 petA 有 ["greet", "play"]
        var typesB = GetInteractionAssetTypes(petB);  // 例如 petB 只有 ["greet"]
        return typesA.Intersect(typesB).ToList();      // 交集 → ["greet"]，即可觸發打招呼
    }
    
    public bool CanInteract(Pet petA, Pet petB)
    {
        return GetAvailableInteractionTypes(petA, petB).Any();  // 只要有交集就能互動
    }
}
```

- **有交集** → 依交集中的類型觸發對應互動動畫（例如只有 `greet` 交集，就只會打招呼，不會一起玩耍）
- **無交集**（例如使用者自訂圖樣完全沒提供任何互動素材，或雙方類型剛好都不同）→ 自動 fallback 為兩隻各自獨立行動，不強迫顯示互動、不報錯

#### 6.5.4 互動行為觸發條件
| 互動類型 | 觸發條件 |
|---------|---------|
| `greet` 打招呼 | 兩者距離 < 100px 且雙方閒置中 |
| `play` 一起玩耍 | 使用者手動觸發，或隨機事件（雙方距離接近時） |
| `cuddle` 依偎互動 | 兩者長時間（例如 > 10 分鐘）維持在接近距離 |

> 以上皆為**選配**，只要缺少對應素材就自動略過，不影響基礎功能運作。

#### 6.5.5 架構影響摘要
| 項目 | 單寵物 | 雙寵物差異 |
|------|--------|-----------|
| 視窗 | 1 個 | 2 個獨立視窗 |
| 狀態存檔 | `pets[0]` | `pets[0]`, `pets[1]` |
| 拖曳/點擊判定 | 單一 hit-test | 各視窗獨立 hit-test，互不影響 |
| 自動保存 | 存單筆 | 存整個陣列 |
| 互動邏輯 | 不適用 | `Pet Coordinator` 定時檢查距離與素材 |

---

### 6.6 統一素材管理中心 (Asset Management Center)

**設計決策** (2026-08-27 確認):
- 音效自訂範圍：**全部開放**（點擊、進食、睡眠、背景音樂皆可替換）
- 介面形式：**單一設置面板**，圖片與音效分區顯示（非分頁、非拆兩個獨立視窗）
- 音效作用範圍：互動音效（點擊/進食/睡眠）**每隻寵物獨立**；背景音樂**全域共用**（原因見 5.2 節說明，避免雙寵物同時播放互相干擾）
- **背景音樂進階規則**：最多可匯入 **3 首**，系統啟動時（或每次切換時）**隨機挑選其中一首**做全域播放；提供**靜音開關**，靜音時保留已匯入的曲目清單，僅暫停播放

#### 6.6.1 面板整體佈局（文字示意）
```
┌──────────────────────────────────────────┐
│  素材管理中心                        [X]   │
├──────────────────────────────────────────┤
│  目前寵物: [Fluffy ▾]  ← 切換寵物用下拉選單│
├──────────────────────────────────────────┤
│  📷 圖樣          完成度: 4 / 6 類型        │
│  ┌────────┐                               │
│  │ 預覽圖  │  目前使用: builtin_cat        │
│  │  256px │  [選擇內建主題 ▾] [匯入資料夾..]│
│  └────────┘                               │
│  ─ 心情圖片 ─────────────────────────────  │
│  SAD        anim_sad_*      1 個 [+新增][管理]│
│  LOW_ENERGY anim_tired_*    2 個 [+新增][管理]│
│  NEUTRAL ✱  anim_idle_*     3 個 [+新增][管理]│
│  ─ 事件圖片 ─────────────────────────────  │
│  點擊       anim_click_* 🎞1 個 [+新增][管理]│
│  進食       anim_feed_*     0 個 [+新增][管理]│
│  睡眠       anim_sleep_*    0 個 [+新增][管理]│
│  ✱ = 必填；其餘為 0 張時自動 fallback（7.3.4）│
├──────────────────────────────────────────┤
│  🔊 音效（此寵物專屬）                     │
│  點擊音效    [預設 ▾] [匯入...] [▶ 試聽]   │
│  進食音效    [自訂: feed.mp3] [匯入...] [▶]│
│  睡眠音效    [預設 ▾] [匯入...] [▶ 試聽]   │
├──────────────────────────────────────────┤
│  🎵 背景音樂（全域，不分寵物）  🔇 靜音 [ ] │
│  曲目1  [自訂: bgm_lofi.mp3]  [匯入...] [▶]│
│  曲目2  [自訂: bgm_piano.mp3] [匯入...] [▶]│
│  曲目3  [尚未設定]            [匯入...] [ ]│
│  ℹ️ 系統將從已設定的曲目中隨機挑選一首播放  │
├──────────────────────────────────────────┤
│           [還原此寵物預設]  [完成]         │
└──────────────────────────────────────────┘
```

**佈局重點**：
- 頂部有寵物切換下拉選單（僅雙寵物模式顯示），確保單一面板可管理兩隻寵物的個別素材
- 「圖樣」與「音效」用區塊分隔，符合分區顯示的需求
- **圖樣區塊依 7.3 節的 6 種類型逐列呈現**，每列顯示目前單元數與 `[+ 新增]`／`[管理]` 兩個動作。`[管理]` 開啟該類型的單元清單，可個別刪除或調整順序
- **單元數旁以 🎞 圖示標記該類型含 Sprite Sheet 單元**。`[管理]` 清單內每個單元顯示形式與格數（例如「anim_click_1　🎞 8 格 @15fps」），單張則顯示「靜態圖」
- **完成度指示器**（例：`4 / 6 類型`）讓使用者一眼看出還有哪些類型沒圖，呼應 7.3.4 節「只有 NEUTRAL 必填、其餘漸進補齊」的設計
- 單元數欄位本身就是「可擴充」的入口：同一類型按幾次 `[+ 新增]` 就是幾個單元，UI 不需要為單一/多個做兩套流程
- **靜態圖與 Sprite Sheet 共用同一列、同一個 `[+ 新增]` 按鈕**，形式的選擇發生在 6.4.2.1 節的匯入對話框內，面板本身不分兩區
- 背景音樂區塊視覺上明確標示「全域」，避免使用者誤以為是該寵物專屬設定
- 背景音樂提供 **3 個曲目欄位**（非必填），至少設定 1 首即可啟用；靜音開關獨立於曲目設定之外，關閉後保留清單但不播放
- 每個音效欄位都有「試聽」按鈕，匯入後可立即確認效果，不用關閉面板才知道對不對

#### 6.6.2 音效上傳規格
| 項目 | 建議值 | 說明 |
|------|--------|------|
| 格式 | `.mp3`, `.wav` | 主流免費工具皆可匯出 |
| 互動音效時長 | 建議 < 3 秒 | 點擊/進食/睡眠屬短促反饋音，過長會顯得拖沓 |
| 背景音樂時長 | 不限，建議可循環播放 | 系統需支援無縫循環 (loop) |
| 背景音樂數量上限 | **最多 3 首** | 系統隨機挑選其中一首做全域播放，可少於3首 |
| 檔案大小 | 互動音效 < 1MB／背景音樂 < 10MB | 避免載入延遲、控制安裝包大小 |

#### 6.6.3 匯入與試聽流程
```
使用者點擊「匯入...」
   → 開啟系統檔案選擇器（篩選 .mp3/.wav）
   → 驗證檔案大小/格式
   → 若通過：複製到 custom_sounds/ 資料夾，更新對應路徑欄位
   → 若失敗：顯示錯誤訊息（格式不符 / 檔案過大），不覆蓋原設定
使用者點擊「▶ 試聽」
   → 立即播放該音效（不影響目前寵物的實際運行狀態）
使用者點擊「還原此寵物預設」
   → 清空該寵物的 PetSoundSet 與 SkinFolderPath 自訂值，回復系統內建素材
```

#### 6.6.4 架構延伸
```csharp
public interface IPetSoundSource
{
    string GetSoundPath(SoundEventType eventType);  // null 則用系統預設
}

public enum SoundEventType { Click, Feed, Sleep }

// 背景音樂為全域，非 per-pet
public class GlobalAudioSettings
{
    public List<string> BackgroundMusicPaths { get; set; }  // 最多 3 首，可少於 3 首或為空
    public bool IsMuted { get; set; }                        // 靜音不影響已匯入清單
}
```

規則：啟動或切換曲目時，從清單隨機挑一首；靜音或清單為空則不播放。

與 6.4.4 節的 `IPetSkinSource` 介面設計理念一致：上層播放邏輯只呼叫 `GetSoundPath()`，不需要知道音效來源是預設還是使用者自訂，方便未來擴充（例如加入更多可自訂事件類型）。

---

## 🔄 7. 狀態流轉邏輯

### 7.1 狀態更新循環

**拆為雙層計時器**，讓靜態圖與 Sprite Sheet 能並存（見 6.4.5 節）：

```
【狀態 tick】每秒鐘執行 (1 Hz，固定):
1. 依 7.4 節規則更新四項狀態數值
   (飢餓每3分鐘 +1, 能量每5分鐘 -1, 幸福度每10分鐘 -1 起算)
2. 根據狀態計算心情 (7.2.1 節，三分支)
3. 決定當前要播哪個「動畫單元」(7.3.5 節，狀態切換或重抽計時到才重新挑選)
4. 檢查用戶輸入
5. 自動保存 (每5分鐘)

【渲染 tick】頻率動態調整 (1 ~ 15 Hz):
6. 依當前單元的 elapsed 決定播到第幾格，重繪
```

#### 7.1.1 渲染 tick 的動態頻率

渲染頻率**依當前播放單元的格數決定**，不是固定值：

| 當前單元 | 渲染 tick 頻率 |
|---------|--------------|
| `frames == 1`（靜態圖） | **暫停重繪**，僅在單元切換時繪一次 |
| `frames > 1` 且 `loop == true` | 該單元的 `fps`（建議 12–15） |
| `frames > 1` 且 `loop == false` | 該單元的 `fps`，播到最後一格後暫停重繪 |

**這個設計解決了 4.3 節記錄的隱憂**：`AllowsTransparency=True` 會使該視窗改走軟體算圖，30 fps 全視窗軟體算圖是真實的效能瓶頸。動態頻率讓使用者若全用靜態素材，效能特性與純靜態架構相同；只有真正在播動畫的那幾秒才吃 CPU。

> 桌寵動畫 **12–15 fps 已足夠**，且比 30 fps 更有手繪感。10.1 節的效能建議據此調整。

> ⚠️ `Hunger` 語意為**飢餓度**，數值越高越餓，因此隨時間**遞增**。寫成遞減會使飢餓度一路降到 0，`SAD` 分支永遠觸發不到。

```
啟動時額外執行一次:
0. 離線凍結處理 (7.4.4 節)：四項數值全部凍結、不補算，僅將 LastTickTime 重設為現在時刻
```

---

### 7.2 心情判定規則

#### 7.2.1 判定情緒分支

**設計決策** (2026-08-27 確認)：由原本的 4 分支收斂為 **3 分支**。

```
if (Hunger > 70)        → SAD
else if (Energy < 20)   → LOW_ENERGY
else                    → NEUTRAL
```

判定順序不可調換：**飢餓優先於低能量**。當 `Hunger > 70` 且 `Energy < 20` 同時成立時，顯示 `SAD`。

```csharp
public PetMood EvaluateMood(Pet pet)
{
    if (pet.Hunger > 70) return PetMood.Sad;
    if (pet.Energy < 20) return PetMood.LowEnergy;
    return PetMood.Neutral;
}
```

> **連帶影響**：幸福度（Happiness）**維持純數值指標，不影響寵物外觀**。曾評估的「在 `NEUTRAL` 內部依 Happiness 調整抽圖權重」折衷方案**不採用**。
>
> 5.1 節保留 `Happiness` 欄位、6.2 節狀態面板保留 `😊 Mood` 顯示，其數值變化規則見 **7.4 節**。它的用途是飼養回饋與 Phase 3 成就/進化系統的輸入值，與 7.3 節的圖片選擇完全解耦。

---

### 7.3 動畫系統

**設計決策** (2026-08-27 確認)：
- 圖片分為 **心情圖片**（3 類，由 7.2.1 自動判定）與 **事件圖片**（3 類，由使用者操作觸發）
- **每一種類型皆可擴充為多張，不限於心情圖片**。單張時直接播放該張，多張時由系統隨機挑選其中一張播放
- 新增圖片不需修改設定檔、不需重新編譯（純資料驅動，與 6.5.2 節互動類型、12 節語言檔的作法一致）

#### 7.3.1 心情與動畫對照表

| 心情分支 | 判定條件 | 檔名前綴 | 目前圖片檔名 | 目前張數 |
|---------|---------|---------|-------------|---------|
| `SAD` | `Hunger > 70` | `anim_sad` | `anim_sad_1` | 1 |
| `LOW_ENERGY` | `Energy < 20` | `anim_tired` | `anim_tired_1`<br>`anim_tired_2` | 2 |
| `NEUTRAL` | 以上皆非 | `anim_idle` | `anim_idle_1`<br>`anim_idle_2`<br>`anim_idle_3` | 3 |

> 上表的「目前張數」是**現況快照，非上限**。任一類型都可再增加圖片（例如日後補上 `anim_sad_2`、`anim_sad_3`），系統會自動納入隨機挑選範圍，程式碼與設定檔皆不需改動。

#### 7.3.2 事件與動畫對照表

| 事件 | 觸發來源 | 檔名前綴 | 目前圖片檔名 | 持續時間 |
|------|---------|---------|-------------|---------|
| 點擊 | 滑鼠點擊寵物 | `anim_click` | `anim_click_1` | 1.5 秒 |
| 進食 | 右鍵選單「餵食」 | `anim_feed` | `anim_feed_1` | 2.5 秒 |
| 睡眠 | 右鍵選單「睡眠」／自動入睡 | `anim_sleep` | `anim_sleep_1` | 持續至醒來 |

**優先權**：`事件圖片 > 心情圖片`。事件播畢後重跑 7.2.1 判定，回到對應心情。

> **持續時間的語意改為「至少 N 秒」，而非「剛好 N 秒」。**
>
> 原定義在靜態圖架構下沒問題，但單元若為 Sprite Sheet（6.4.5 節），動畫有自己的自然長度。若 `CLICK` 訂死 1.5 秒而動畫實際需 2 秒，動畫會被砍在中間。
>
> 新規則：`實際持續時間 = max(durationSec, 動畫自然長度)`。動畫自然長度 = `frames / fps`，`loop: true` 的單元視為 0（由 `durationSec` 決定）。

**事件互相衝突時採「進行中不被打斷」**：例如 `FEED` 播放期間忽略新的 `CLICK`，避免進食動作被點擊蓋掉。

> `anim_sleep_*`（已睡著）與 `anim_tired_*`（想睡但清醒）是**不同素材**，不可混用。前者對應 `SLEEP` 事件，後者對應 `LOW_ENERGY` 心情。

#### 7.3.3 檔名規則

統一格式：

```
anim_[前綴]_[序號].png
```

- 序號從 `1` 起，系統以萬用字元掃描（例如 `anim_idle_*.png`）
- **不要求連號**：使用者刪掉 `anim_idle_2` 但保留 `anim_idle_3` 時，系統仍正常運作，不報錯
- 掃描到幾張就是幾張，**新增圖片只要放進圖樣資料夾即生效**，不需改設定、不需重編譯
- 素材格式延續 6.4.3 節規格（PNG／JPG、< 2MB、建議 256×256），所有類型共用同一套驗證邏輯

**圖樣資料夾範例**（`custom_skins/custom_001/`）：

```
custom_skins/custom_001/
├── anim_idle_1.png        ← NEUTRAL（唯一必填）
├── anim_idle_2.png
├── anim_idle_3.png
├── anim_sad_1.png         ← SAD
├── anim_tired_1.png       ← LOW_ENERGY
├── anim_tired_2.png
├── anim_click_1.png       ← 點擊事件
├── anim_feed_1.png        ← 進食事件
├── anim_sleep_1.png       ← 睡眠事件
├── interaction_greet.png  ← 6.5.2 節的互動素材，同一資料夾
└── skin.json              ← 各單元格數描述（6.4.5 節）
```

> **檔名慣例掃描只決定「有哪些單元」，不決定「每個單元有幾格」。** 格數資訊記在同資料夾的 `skin.json`（見 6.4.5 節），由匯入流程自動維護。無 `skin.json` 時全部視為單格，既有素材零遷移。

**設定檔 `pet_visuals.json`**（登記類型清單，開發者擴充新類型時只改這裡）：

```json
{
  "visuals": [
    { "code": "SAD",        "kind": "mood",  "prefix": "anim_sad",   "required": false, "fallback": "NEUTRAL",    "rerollIntervalSec": 0 },
    { "code": "LOW_ENERGY", "kind": "mood",  "prefix": "anim_tired", "required": false, "fallback": "NEUTRAL",    "rerollIntervalSec": 0 },
    { "code": "NEUTRAL",    "kind": "mood",  "prefix": "anim_idle",  "required": true,  "fallback": null,         "rerollIntervalSec": 8 },
    { "code": "CLICK",      "kind": "event", "prefix": "anim_click", "required": false, "fallback": null,         "durationSec": 1.5 },
    { "code": "FEED",       "kind": "event", "prefix": "anim_feed",  "required": false, "fallback": null,         "durationSec": 2.5 },
    { "code": "SLEEP",      "kind": "event", "prefix": "anim_sleep", "required": false, "fallback": "LOW_ENERGY", "durationSec": 0, "rerollIntervalSec": 20 }
  ],
  "weather": {
    "enabled": false,
    "weatherChance": 0.3,
    "pollIntervalMin": 30,
    "codes": ["clear", "cloudy", "rain", "snow", "thunder", "fog"]
  }
}
```

**欄位說明**：

| 欄位 | 說明 |
|------|------|
| `code` | 狀態代號，對應 `PetVisualState` 列舉 |
| `kind` | `mood`（由 7.2.1 判定）或 `event`（由操作觸發） |
| `prefix` | 檔名前綴。**注意：心情代號與前綴不是一對一**（`LOW_ENERGY` → `anim_tired`），因此必須寫在設定檔，不可用列舉名稱轉小寫推導 |
| `required` | 是否必填。僅 `NEUTRAL` 為 `true` |
| `fallback` | 缺圖時退回哪個狀態，`null` 代表不換圖 |
| `durationSec` | 事件持續秒數，`0` 代表持續型（睡眠，直到條件解除） |
| `rerollIntervalSec` | 多張時的重抽間隔秒數，`0` 代表進入狀態時抽一次就不再重抽 |

#### 7.3.4 缺圖時的 fallback 鏈

延續 6.5 節「漸進式增強」精神——缺素材不報錯、不卡住，自動退而求其次：

| 缺少的類型 | 行為 |
|-----------|------|
| `SAD` | 退回 `NEUTRAL` |
| `LOW_ENERGY` | 退回 `NEUTRAL` |
| `SLEEP` | 退回 `LOW_ENERGY`，若也缺則再退 `NEUTRAL` |
| `CLICK` | 不換圖，維持目前畫面 |
| `FEED` | 不換圖，維持目前畫面 |
| `NEUTRAL` | **整套圖樣視為不合法**，於 6.4.3.1 節匯入驗證階段即擋下 |

因此使用者最少只要準備 **1 張** `anim_idle_1.png` 就能開始使用，其餘慢慢補。

#### 7.3.5 選圖與播放邏輯

**責任邊界重新劃分。**

「決定播哪個」與「維持現狀不換」若由同一個類別負責，會**把 Sprite Sheet 凍結在第一格**——多格動畫的需求剛好相反：同一個單元內，每次呼叫要回傳**下一格**。因此拆開：

| 元件 | 責任 | 不負責 |
|------|------|--------|
| `PetVisualSelector` | 決定現在該播**哪個動畫單元** | 該單元播到第幾格 |
| `IPetSkinSource` 實作 | 依 `elapsed` 決定**第幾格** | 該播哪個單元 |

這樣靜態圖與 Sprite Sheet 就不需要兩套邏輯：靜態實作永遠回傳第 1 格，多格實作依時間推進。

##### 單元選擇（PetVisualSelector）

**關鍵：抽籤發生在「單元切換的時機」，不是每次重繪。**

7.1 節的狀態 tick 是 1 Hz。若每次 tick 都重抽，擁有多個單元的狀態會**每秒換一次**，看起來像故障。只在下列兩種時機重抽：

1. 狀態發生改變（心情切換、事件觸發）
2. 該狀態設有 `rerollIntervalSec` 且計時已到

```csharp
public enum PetVisualState { Neutral, Sad, LowEnergy, Click, Feed, Sleep }

public class PetVisualSelector
{
    private readonly Dictionary<PetVisualState, List<string>> _pool;  // 掃描資料夾建立
    private readonly Random _rng = new Random();
    private PetVisualState _currentState;
    private string _currentUnit;
    private DateTime _unitStartTime;   // 供渲染層計算 elapsed
    private DateTime _lastRollTime;

    /// <summary>決定當前該播哪個動畫單元。不決定播第幾格。</summary>
    public string ResolveUnit(PetVisualState state, int rerollIntervalSec)
    {
        bool stateChanged = state != _currentState;
        bool needReroll = rerollIntervalSec > 0
            && (DateTime.Now - _lastRollTime).TotalSeconds >= rerollIntervalSec;

        if (!stateChanged && !needReroll)
            return _currentUnit;          // ← 維持同一個「單元」，但該單元內部仍會逐格推進

        _currentState    = state;
        _lastRollTime    = DateTime.Now;
        _unitStartTime   = DateTime.Now;  // ← 重設動畫時間軸，讓新單元從第 1 格開始
        _currentUnit     = Pick(state);
        return _currentUnit;
    }

    public TimeSpan ElapsedInUnit => DateTime.Now - _unitStartTime;

    private string Pick(PetVisualState state)
    {
        var list = _pool.GetValueOrDefault(state);

        if (list == null || list.Count == 0)
            return ResolveFallback(state);            // 缺素材 → 走 7.3.4 的 fallback 鏈

        if (list.Count == 1)
            return list[0];                           // 單一單元：直接播它

        // 多個單元：隨機挑一個，且避免連續兩次抽到同一個
        var candidates = list.Where(u => u != _currentUnit).ToList();
        if (candidates.Count == 0) candidates = list;
        return candidates[_rng.Next(candidates.Count)];
    }
}
```

> **`_unitStartTime` 是讓兩種素材共存的關鍵**。沒有它，Sprite Sheet 無法知道自己播到哪裡；有了它，「維持現狀」的早退分支就只凍結**單元選擇**，不凍結**格數推進**——這正是讓兩種素材共存的那一行。

##### 格數決定（IPetSkinSource 實作）

```csharp
// 靜態圖：忽略 elapsed，永遠回傳整張圖
public FrameRef GetFrame(PetVisualState state, TimeSpan elapsed)
{
    var bmp = LoadUnit(_currentUnit);
    return new FrameRef { Source = bmp, Rect = new Int32Rect(0, 0, bmp.PixelWidth, bmp.PixelHeight) };
}

// Sprite Sheet：依 elapsed 推算格號
public FrameRef GetFrame(PetVisualState state, TimeSpan elapsed)
{
    var unit = _units[_currentUnit];          // 來自 skin.json
    var bmp  = LoadUnit(_currentUnit);

    int idx = (int)(elapsed.TotalSeconds * unit.Fps);
    idx = unit.Loop
        ? idx % unit.Frames                    // 循環
        : Math.Min(idx, unit.Frames - 1);      // 播完停在最後一格

    return new FrameRef {
        Source = bmp,
        Rect   = new Int32Rect(idx * unit.FrameWidth, 0, unit.FrameWidth, bmp.PixelHeight)
    };
}
```

**設計註記**：
- `list.Count == 1` 那一行在數學上可省略（`_rng.Next(1)` 必然回傳 `0`），但明寫出來能讓「單一單元就播它」這條規則在程式碼裡看得見
- 「避免連續抽到同一個」是刻意加的：`NEUTRAL` 每 8 秒重抽一次，若連兩次抽中同一個單元，視覺上等同沒換，使用者會以為隨機功能壞了
- **所有單元走同一段程式碼**。單一與多個、靜態與多格皆無分支差異，這是「每種類型都能擴充」在實作上的保證

#### 7.3.6 資源載入策略

配合 10.1 節「延遲載入資源」與 50–100 MB 記憶體上限：

- 圖樣資料夾**只在啟動時掃描檔名建立索引**，不預先載入所有點陣圖
- 實際 `BitmapImage` 於首次抽中時才載入，之後以 LRU 快取保留（建議上限 12 張／每隻寵物）
- 雙寵物模式時兩隻各自獨立快取，互不共用（因為可能使用不同圖樣）
- 單張 256×256 PNG 解碼後約 256 KB，即使每類型都擴充到 5 個單元（共 30 個）也僅約 7.5 MB，記憶體不是限制因素——限制因素是**啟動速度**，所以延遲載入的重點在縮短冷啟動時間

**Sprite Sheet 對快取反而有利**：一個 8 格單元是**單一檔案、單次 I/O**，比 8 張獨立 PNG 快得多，且底圖只需解碼一次，播放時切格只是換矩形座標（見 6.4.4 節 `FrameRef`），不重新配置記憶體。

記憶體上限估算需以**格數**而非單元數計：8 格 256×256 單元解碼後約 2 MB。LRU 快取上限建議由「12 個單元」改為「**總計 48 格**」，避免使用者全用 16 格動畫時撞破 10.1 節的上限。

---

### 7.4 狀態數值變化規則

**設計決策** (2026-08-27 確認)：幸福度採「**自然衰減 + 操作回補**」模型。長時間不餵食、不讓睡覺或不互動會慢慢扣減，餵食與互動會增加。

#### 7.4.1 四項狀態總覽

| 狀態 | 語意方向 | 自然變化 | 程式關閉時 | 影響 |
|------|---------|---------|-----------|------|
| `Hunger` 飢餓度 | **越高越餓** | 每 3 分鐘 **+1** | **凍結** | 7.2.1 心情判定（> 70 → SAD） |
| `Energy` 能量 | 越低越累 | 每 5 分鐘 **-1** | **凍結** | 7.2.1 心情判定（< 20 → LOW_ENERGY） |
| `Happiness` 幸福度 | 越高越好 | 見 7.4.2 | **凍結** | 不影響外觀，面板顯示與 Phase 3 成就 |
| `Health` 健康度 | 越高越好 | 見 7.4.5 | **凍結** | 面板顯示與 Phase 3 成就 |

所有數值皆夾在 `0 ~ 100` 之間。

> **凍結的設計理由**：四項狀態反映的都是「照顧」的結果，而使用者沒開程式時根本沒有照顧的機會——任何一項離線繼續變化，都等於懲罰（或獎勵）他沒開軟體。因此**程式關閉期間四項數值全部凍結**，重開時如同時間未曾流逝。附帶好處是離線期間不累計任何變化，改系統時鐘也無從刷數值。詳見 7.4.4 節。

#### 7.4.2 幸福度衰減規則

採「基礎衰減 + 條件加成」，各項**可疊加**，以**小時**為結算單位：

| 衰減來源 | 條件 | 扣減速率 |
|---------|------|---------|
| 基礎自然衰減 | 恆常 | 每小時 **-1** |
| 飢餓懲罰 | `Hunger > 70` | 額外每小時 **-1** |
| 疲勞懲罰 | `Energy < 20` | 額外每小時 **-1** |
| 冷落懲罰 | 累計未互動超過 **4 小時** | 額外每小時 **-2** |

**完全放置不管**：三項懲罰隨時間陸續生效（飢餓 3.5h、冷落 4h、疲勞 6.7h），最終達每小時 -5，由 100 降至 0 約需 **24 小時**。
**持續照顧**：僅基礎衰減每小時 -1，且餵食與互動的回補（7.4.3）遠大於此，實際上會維持高檔。

> **「一整天不理它才會歸零」是刻意的節奏**。原設計為每 10 分鐘結算、最壞 4 小時歸零，實測過快——使用者上個班回來就看到寵物幸福度見底，變成壓力而非陪伴。

##### 冷落計時只在程式執行時累加

冷落懲罰**不可**直接用 `LastInteractionTime` 與現在時刻的差值判定。若程式關了三天，重開後該差值立刻超過 4 小時，幸福度會以最快速率狂掉——這會讓 7.4.4 的凍結形同虛設。

正確作法：維護一個 `AwakeIdleSeconds` 累計欄位，**只在程式執行時累加**，任何互動時歸零。程式關閉期間不累加。

```csharp
// 狀態 tick（1 Hz）內
pet.AwakeIdleSeconds += 1;

// 任何互動發生時
pet.AwakeIdleSeconds = 0;
pet.LastInteractionTime = DateTime.Now;   // 此欄位仍保留，供 7.4.3 冷卻判定用
```

#### 7.4.3 幸福度增加規則

| 操作 | 增加值 | 冷卻時間 | 對應事件 |
|------|-------|---------|---------|
| 餵食 | **+10** | 30 分鐘 | 6.3 節右鍵選單「餵食」 |
| 點擊 / 玩耍 | **+2** | 60 秒 | 7.3.2 節 `CLICK` 事件、選單「玩耍」 |
| 睡眠完成（`Energy` 回滿） | **+5** | 無 | 7.3.2 節 `SLEEP` 事件結束 |
| 雙寵物互動觸發 | **+3** | 30 分鐘 | 6.5.4 節互動行為，兩隻各自 +3 |

> **冷卻機制的用途是防止數值失去意義**：若點擊無冷卻，使用者連點 50 下即可瞬間補滿。
>
> **重要**：冷卻期間**照常可以操作**——動畫照播、音效照響、`AwakeIdleSeconds` 照歸零（因此仍能解除冷落懲罰），只是**不再累加幸福度**。不可實作成「冷卻中禁止餵食/點擊」。

#### 7.4.4 離線期間的凍結

程式關閉期間，**四項數值（`Hunger` / `Energy` / `Happiness` / `Health`）全部凍結不變**，不做任何離線補算。重開程式時，寵物狀態與關閉當下完全相同，如同關閉期間時間未曾流逝。

- **離線期間一律不執行**任何變化：7.4.1 的 `Hunger` / `Energy` 自然變化、7.4.2 幸福度衰減、7.4.3 幸福度增加、7.4.5 健康度判定，全部略過
- `AwakeIdleSeconds` / `HealthCheckSeconds` 不因離線時間增加（本就只在執行時累加）
- 啟動時僅將 `LastTickTime` 重設為現在時刻，作為執行期狀態 tick 的基準
- 因離線期間不累計任何變化，**改系統時鐘也無從刷數值**，不再需要額外的時鐘回調防護與 24 小時上限

```csharp
// 啟動時呼叫：離線期間全部凍結，不補算任何數值，只重設狀態 tick 基準
public void ApplyOfflineFreeze(Pet pet, DateTime now)
{
    // Hunger / Energy / Happiness / Health 全部保持不變
    pet.LastTickTime = now;   // 重設為現在時刻，供執行期狀態 tick 使用
}
```

#### 7.4.5 健康度規則

每 **30 分鐘執行時間**檢查一次（程式關閉期間不計時）：

```
if (Hunger > 90 || Energy < 10 || Happiness < 20)   → Health -1
else if (Hunger < 30 && Energy > 70 && Happiness > 70) → Health +1
```

- 長期照顧不佳緩慢損害健康，照顧良好緩慢恢復
- 刻意設計成比其他三項更慢：連續惡劣條件下由 100 降至 0 需約 **50 小時執行時間**，是長期指標而非短期波動
- 兩個條件都不成立時（一般狀態）健康度不變
- 程式關閉時凍結，與 `Happiness` 同理（見 7.4.1）

#### 7.4.6 資料模型異動

5.1 節 `Pet` 類別需新增三個欄位：

```csharp
public DateTime LastTickTime { get; set; }   // 上次狀態結算時刻；離線期間全部凍結（7.4.4），啟動時重設為現在時刻
public int AwakeIdleSeconds { get; set; }    // 執行期間累計未互動秒數，7.4.2 冷落懲罰判定
public int HealthCheckSeconds { get; set; }  // 執行期間累計秒數，7.4.5 每 30 分鐘結算
```

冷卻判定沿用既有欄位：餵食冷卻用 `LastFedTime`、互動冷卻用 `LastInteractionTime`。

> 這兩個新欄位刻意存「**累計秒數**」而非時間戳記，正是為了實現凍結——時間戳記會隨真實時間推進，累計值只在程式執行時才增加。

---

### 7.5 天氣連動

依所在地即時天氣，在寵物平常狀態下隨機播放對應的天氣素材。

#### 7.5.1 運作規則

1. 每 **30 分鐘**查詢一次天氣（避免 API 頻率限制），對應到天氣代號
2. 心情為 `NEUTRAL` 時，以 `weatherChance`（預設 **30%**）的機率改播天氣單元，其餘時間照常播 `anim_idle_*`
3. 掃描 `anim_weather_{代號}_*.png`，**找不到素材則此功能對該套圖樣停用**，行為與未啟用時完全相同
4. 多個單元時的隨機挑選、重抽時機皆沿用 7.3.5 節，不另立規則

**只在 `NEUTRAL` 生效**：寵物餓了或累了時，那個狀態比天氣重要。天氣是氛圍，不該蓋過生理狀態。這樣天氣也不需要新增優先權層級——它只是 `NEUTRAL` 選單元時多出來的候選。

#### 7.5.2 天氣代號

| 代號 | 對應天氣 | 檔名前綴 |
|------|---------|---------|
| `clear` | 晴 | `anim_weather_clear` |
| `cloudy` | 陰、多雲 | `anim_weather_cloudy` |
| `rain` | 雨 | `anim_weather_rain` |
| `snow` | 雪 | `anim_weather_snow` |
| `thunder` | 雷雨 | `anim_weather_thunder` |
| `fog` | 霧 | `anim_weather_fog` |

代號可增減，於 `pet_visuals.json` 登記，新增不需改程式。素材規格與 6.4.3 節相同，可為靜態圖或 Sprite Sheet。

#### 7.5.3 停用條件

下列任一情況即靜默停用，不顯示錯誤、不影響正常運作：

| 情況 | 處理 |
|------|------|
| 該套圖樣無對應天氣素材 | 照常播 `anim_idle_*` |
| 使用者未開啟此功能 | 預設**關閉**（需網路與位置資訊） |
| 無網路 / API 逾時 / 查詢失敗 | 沿用上次結果，超過 3 小時未更新則停用 |
| 天氣代號未定義於 `pet_visuals.json` | 視為無素材 |

> **預設關閉是刻意的**。此功能需要網路連線與所在地資訊，屬隱私敏感項目，應由使用者在設定中主動啟用。定位建議用 IP 粗略定位或讓使用者手動指定城市，不索取系統定位權限。API 可用 Open-Meteo 這類免金鑰服務。

---

## 💾 8. 存儲與持久化

### 8.1 文件位置
```
%APPDATA%\DesktopPet\
├── pet_data.json          (寵物數據，含每隻寵物的音效/圖樣自訂路徑)
├── settings.json          (設置，含全域背景音樂)
├── achievements.json      (成就)
├── skins.json             (已匯入的自訂圖樣清單)
├── sounds.json             (已匯入的自訂音效清單)
├── pet_visuals.json        (7.3.3 節：圖片類型登記檔)
└── assets/
    ├── sounds/           (系統內建預設音效)
    ├── themes/           (內建官方主題，隨程式打包)
    │   ├── builtin_cat/      ← 每套主題為一個資料夾
    │   │   ├── anim_idle_1.png
    │   │   ├── anim_sad_1.png
    │   │   ├── skin.json      ← 各單元格數描述（6.4.5節）
    │   │   └── ... (見 7.3.3 節命名規則)
    │   └── builtin_dog/
    ├── custom_skins/      (使用者匯入的自訂圖樣，複製存放於此)
    │   └── custom_001/       ← 每套圖樣一個資料夾，內含 anim_*.png + skin.json
    └── custom_sounds/     (使用者匯入的自訂音效，複製存放於此)
```

> 資料夾名稱即 `skinId`。

> **注意**: 使用者匯入的圖片/音效皆會複製到對應的 `custom_skins/{skinId}/` / `custom_sounds/` 資料夾，而非直接引用原始路徑，避免使用者移動/刪除原檔導致素材遺失。

### 8.2 備份策略
- 每次關閉前自動保存
- 每 5 分鐘自動保存一次
- 保留最後 3 個備份版本

---

## 🚀 9. 開發進度追蹤

### 9.1 Phase 1: MVP (預計 3-4 週)
- [ ] 項目初始化 & UI 框架 (WPF + .NET 8，見 4.3 節)
- [ ] **Git 儲存庫初始化** (13 節：.gitignore、分支策略)
- [ ] 基礎寵物渲染 (7.3 節：3 心情 + 3 事件圖片類型，每類型至少 1 張)
- [ ] **多單元隨機挑選機制** (7.3.5 節：含重抽時機控制、避免連續重複)
- [ ] **雙層計時器 + 動態渲染頻率** (7.1.1 節)
- [ ] **Sprite Sheet 支援** (6.4.5 節：skin.json、FrameRef、匯入分支與整除驗證)
- [ ] **多寵物窗口管理**（支援 1-2 個獨立窗口實例）
- [ ] 滑鼠交互 (點擊、拖曳，each需獨立判定所屬寵物)
- [ ] 基本狀態系統 (4 項屬性，每隻寵物獨立)
- [ ] **狀態數值變化規則** (7.4 節：飢餓/能量/幸福度衰減與回補、冷卻機制)
- [ ] **離線凍結** (7.4.4 節：關閉期間四項數值全部凍結，啟動時重設 LastTickTime)
- [ ] 保存 / 加載機制
- [ ] 右鍵菜單 (4-5 個基本功能)

### 9.2 Phase 2: 完整化 (預計 2-3 週)
- [ ] 音效系統
- [ ] 進度統計 (飼養天數、等級)
- [ ] 系統托盤集成
- [ ] 更豐富的素材 (各類型由 1 個擴充至多個單元，並混入 Sprite Sheet，驗證 6.4.5 混用)
- [ ] 天氣連動 (7.5 節：查詢、代號對應、素材缺漏時靜默停用)
- [ ] 簡單主題切換
- [ ] **多語言支援**（詳見第 12 節）：繁中/英/日三語系 + 語言檔架構

### 9.3 Phase 3: 高級功能 (可選)
- [ ] 小遊戲模塊
- [ ] 進化系統
- [ ] 成就解鎖
- [ ] 社交分享

---

## 🔍 10. 開發注意事項

### 10.1 性能最佳實踐
- ✓ 使用 Timer 而不是線程不斷檢查
- ✓ **雙層計時器**（7.1 節）：狀態 tick 固定 1 Hz；渲染 tick 動態 1–15 Hz
- ✓ 播放單格單元時**暫停重繪**，僅單元切換時繪一次；播多格動畫才升至該單元 fps
- ✓ 動畫速率建議 **12–15 fps**，不使用 30 fps——`AllowsTransparency=True` 下為軟體算圖（見 4.3 節），高頻全視窗重繪是實質瓶頸
- ✓ 內存占用控制在 50-100 MB 以下
- ✓ 延遲加載資源（7.3.6 節：僅掃描檔名建索引，首次抽中才解碼點陣圖，LRU 快取上限 **總計 48 格**/隻）

### 10.2 Windows 特定考量
- ✓ 實現透明窗口 (WS_EX_LAYERED)
- ✓ 防止被最小化時消失 (特殊窗口樣式)
- ✓ 處理多監視器支持
- ✓ 避免遮擋任務欄

### 10.3 跨版本兼容性
- ✓ 測試 Windows 10 (1809+) / 11
- ✓ DPI 感知支持（WPF + .NET 8 可用 Per-Monitor V2）
- ✓ 處理高分辨率屏幕

---

## 📝 11. 資源清單

### 11.1 需要準備的素材
- [ ] 寵物角色設計 (多個朝向)
- [ ] **內建官方主題 2 套**，每套依 7.3 節需涵蓋 6 種圖片類型 (256x256 PNG，透明背景，由開發者比照使用者匯入規格製作)

  | 類型 | 檔名 | 最低單元數 | 建議單元數 | 建議形式 |
  |------|------|-----------|-----------|---------|
  | `NEUTRAL` ✱ | `anim_idle_*.png` | 1 | 3 | 混用：1 靜態 + 2 動畫 |
  | `SAD` | `anim_sad_*.png` | 0 | 1 | 靜態 |
  | `LOW_ENERGY` | `anim_tired_*.png` | 0 | 2 | 混用 |
  | 點擊 | `anim_click_*.png` | 0 | 1 | 動畫（8 格 @15fps，不循環） |
  | 進食 | `anim_feed_*.png` | 0 | 1 | 動畫（不循環） |
  | 睡眠 | `anim_sleep_*.png` | 0 | 1 | 動畫（2 格 @2fps，循環，呼吸感） |
  | 天氣（選用） | `anim_weather_{代號}_*.png` | 0 | 依需要 | 靜態或動畫 |
  | **合計／套** | | **1** | **9** | |

  ✱ 僅 `NEUTRAL` 必填，其餘缺圖時走 7.3.4 節 fallback 鏈。
  **分階段作法**：若首發工作量吃緊，可先各畫 `anim_idle_1` + `anim_sad_1`（2 張／套，共 4 張）驗證流程，其餘依 fallback 自動退回 `NEUTRAL`，後續版本再補齊至建議張數。

  > **內建主題適合用來展示 Sprite Sheet**。6.4.1 節要求內建主題比照使用者規格製作，並存架構下這代表內建主題可自由混用兩種形式——建議至少讓 `anim_click_1` 和 `anim_sleep_1` 做成連續動畫，作為 6.4.2.1 節匯入功能的實際範例。

  > **風格方向：暫不決定**。由於 6.4.1 節已確立「內建主題比照使用者規格製作、共用同一套驗證與渲染邏輯」，風格屬於純美術決策，不影響任何程式架構，可延後至實際製作素材前再定。開發期間可先用色塊或草稿圖佔位。

- [ ] 系統預設音效 (.wav / .mp3，皆為使用者可自訂替換之預設值，詳見 6.6 節)
  - 點擊反應聲
  - 進食聲
  - 睡眠聲
  - 背景音樂 (預設可關閉)
- [ ] 圖標 & UI 元素

### 11.2 參考資源
- Windows API 文檔
- WPF 官方示例
- 開源桌面寵物項目 (參考)

---

## 🌐 12. 多語言在地化設計

**設計決策** (2026-08-27 確認):
- 首發支援 **3 種語言**：繁體中文、英文、日文
- **架構需支援未來擴充**其他語言，且擴充時**不需重新編譯程式**（純資料驅動）

### 12.1 語言資源架構
採用**外部語言檔**而非寫死在程式碼內的 `.resx`，方便日後只靠新增檔案就能擴充語言：

```
Resources/Localization/
├── zh-TW.json     (繁體中文，首發，同時作為 fallback 預設語言)
├── en-US.json     (英文，首發)
├── ja-JP.json     (日文，首發)
└── (未來可直接新增 ko-KR.json、fr-FR.json... 不需改程式碼)
```

**語言檔格式範例** (`zh-TW.json`):
```json
{
  "language_code": "zh-TW",
  "language_display_name": "繁體中文",
  "strings": {
    "menu.feed": "餵食",
    "menu.play": "玩耍",
    "menu.sleep": "睡眠",
    "menu.settings": "設置",
    "asset_panel.title": "素材管理中心",
    "asset_panel.current_pet": "目前寵物",
    "error.invalid_format": "不支援此檔案格式，請使用 PNG 或 JPG 圖片",
    "error.file_too_large": "圖片檔案過大（目前 {0} MB），請壓縮至 {1}MB 以內"
  }
}
```

### 12.2 語言載入與切換邏輯

```csharp
public class LocalizationManager
{
    private const string FallbackLanguage = "zh-TW";

    // 啟動時掃描資料夾產生語言選單，不寫死語言清單
    public List<string> ScanAvailableLanguages() => /* 掃 Resources/Localization/*.json */;

    // 鍵值缺漏時逐層退回，不顯示空白也不報錯
    public string GetString(string key, string lang) =>
        _loaded[lang].GetValueOrDefault(key)
        ?? _loaded[FallbackLanguage].GetValueOrDefault(key)
        ?? $"[{key}]";   // 連 fallback 都沒有時顯示鍵值本身，方便除錯
}
```

> **不提供「翻譯範本產生工具」**：語言檔為純 JSON，複製 `en-US.json` 改內容即可，工具的便利性不足以抵銷維護成本。

**擴充新語言的流程**（給未來的開發者或社群翻譯者）：
1. 複製 `en-US.json` 作為範本
2. 翻譯 `strings` 內所有值，`language_code` 改為新語言代碼（例如 `ko-KR`）
3. 將檔案放入 `Resources/Localization/` 資料夾
4. 重啟程式即可在語言選單看到新選項，**不需要重新編譯**

### 12.3 需要在地化的範圍
| 範圍 | 說明 |
|------|------|
| UI 文字 | 選單、按鈕、面板標題、設置選項 |
| 錯誤訊息 | 6.4.3.1 節的圖片驗證錯誤、音效匯入錯誤等 |
| 寵物對話/提示文字（若有） | 例如飢餓提示、心情文字 |
| 日期時間格式 | 依語言慣用格式顯示飼養天數、匯入日期等 |

> **不需要在地化**：使用者自訂的寵物名稱、匯入的素材檔名——這些是使用者輸入的內容，不受語言切換影響。

### 12.4 架構影響摘要
- `Settings` 資料模型需新增 `CurrentLanguage` 欄位（儲存語言代碼，如 `"zh-TW"`）
- 所有 UI 層的文字顯示都需改為透過 `LocalizationManager.GetString(key)` 取得，禁止字串寫死在 XAML/程式碼中
- 首次啟動時，建議偵測系統語言作為預設值，若系統語言不在支援清單內則 fallback 為繁體中文

---

## 🔧 13. 版本控制與專案管理

採用 **GitHub**。

### 13.1 儲存庫結構

```
DesktopPet/
├── .gitignore
├── README.md
├── docs/desktop_pet_design_doc.md   ← 本設計檔隨程式碼一起版控
├── src/DesktopPet/                  ← 14 節的程式碼結構
└── assets-src/                      ← 素材原始檔（PSD/AI），可選
```

設計檔納入版控的用意：設計決策與實作變更落在同一個 commit，日後查「這規則何時改的、當時程式怎麼改」可直接看 diff。

### 13.2 .gitignore 專案特有部分

除標準 .NET 忽略項（`bin/`、`obj/`、`.vs/`、`*.user`）外，必須加上：

```gitignore
# 執行期使用者資料 —— 絕對不可進版控
**/custom_skins/
**/custom_sounds/
pet_data.json
settings.json
achievements.json
```

> ⚠️ 8.1 節的使用者資料存放於 `%APPDATA%\DesktopPet\`，本就在儲存庫之外。但開發測試時常會把測試存檔或測試素材放進專案資料夾，一旦提交會污染儲存庫，也可能把個人素材推上公開儲存庫。

### 13.3 二進位素材處理

| 素材 | 進版控 | 說明 |
|------|-------|------|
| 內建官方主題 PNG（11.1 節） | ✅ | 隨程式打包，必須版控。2 套約 18 個單元、總量 < 1.5MB |
| 預設音效 | ✅ | 數量少 |
| 素材原始檔（PSD/AI） | ⚠️ | 單檔數十 MB，建議放雲端或 Git LFS |
| 使用者自訂素材 | ❌ | 執行期資料，見 13.2 |

Git 對二進位檔無法做行級差異，每次修改都完整存一份新版本。PNG 若反覆改稿，儲存庫會隨改稿次數線性成長——**定稿前的迭代建議在儲存庫外進行**。

### 13.4 分支與提交

`main` 保持可建置，功能開 `feature/*`、修正開 `fix/*`。依第 9 節的核取項目切分支，一個項目一個分支。

提交訊息帶章節編號，例如 `feat(7.4): 實作幸福度衰減與冷卻機制`，日後對照設計書方便。小步提交，一次只做一件事。

### 13.5 發佈

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

產出 `.exe` 以 **GitHub Releases** 發佈，依語意化版本號打 tag。

---

## 🔗 14. 快速參考

### 常用命令 / 快捷鍵 (待定)
| 操作 | 快捷鍵 |
|------|--------|
| 顯示/隱藏面板 | `Ctrl + P` |
| 打開設置 | `Ctrl + S` |
| 重置寵物 | `Ctrl + R` |
| 截圖 | `Ctrl + F12` |

### 關鍵代碼檔案結構 (預規劃)
```
DesktopPet/
├── MainWindow.xaml(.cs)      (改為可實例化多次，每隻寵物一個窗口)
├── Core/
│   ├── PetCoordinator.cs     (管理1-2隻寵物、跨寵物互動判定)
│   ├── PetInstance.cs        (單一寵物的完整運行單元)
│   ├── PetLogic.cs
│   ├── StateManager.cs        (7.4節：四項數值變化總控)
│   ├── HappinessManager.cs    (7.4.2-7.4.3節：衰減加成 + 增加冷卻)
│   ├── OfflineFreezeHandler.cs  (7.4.4節：離線期間四項數值全部凍結，啟動時重設 LastTickTime)
│   └── AnimationManager.cs
├── Core/Interaction/
│   ├── PetInteractionChecker.cs  (互動素材偵測)
│   └── InteractionRules.cs       (距離/觸發條件定義)
├── UI/
│   ├── SettingsWindow.xaml
│   ├── OnboardingWindow.xaml (首次啟動：選擇養1或2隻)
│   ├── AssetManagementWindow.xaml (6.6節：統一素材管理中心，圖片+音效)
│   └── StatusPanel.xaml
├── Models/
│   ├── Pet.cs                  (含 PetSoundSet)
│   ├── GameState.cs           (Pets改為List<Pet>)
│   └── Settings.cs            (含 GlobalAudioSettings、CurrentLanguage)
├── Utils/
│   ├── StorageManager.cs
│   ├── AudioManager.cs
│   ├── ResourceLoader.cs
│   └── SkinManager.cs        (圖樣匯入/切換/管理)
├── Core/Skins/
│   ├── IPetSkinSource.cs     (素材抽象介面，6.4.4節：GetFrame(state, elapsed) → FrameRef)
│   ├── FrameRef.cs           (6.4.4節：底圖 + 矩形，避免逐格配置記憶體)
│   ├── SkinManifest.cs       (6.4.5節：skin.json 讀寫，無檔案時全視為單格)
│   ├── StaticImageSkinSource.cs  (單格實作：忽略 elapsed，回傳整張圖)
│   └── SpriteSheetSkinSource.cs  (多格實作：依 elapsed 推算格號)
├── Core/Weather/
│   ├── IWeatherProvider.cs   (7.5節：天氣查詢抽象，便於測試時注入假資料)
│   ├── OpenMeteoProvider.cs  (7.5節：實際查詢，30分鐘快取)
│   └── WeatherCodeMapper.cs  (7.5.2節：API 代碼 → 6 種天氣代號)
├── Core/Visuals/
│   ├── PetVisualState.cs     (7.3節：6種狀態列舉)
│   ├── MoodEvaluator.cs      (7.2.1節：3分支心情判定)
│   ├── PetVisualSelector.cs  (7.3.5節：挑選「動畫單元」+ 重抽時機 + _unitStartTime)
│   ├── RenderTickController.cs (7.1.1節：依當前單元格數動態調整重繪頻率)
│   ├── VisualRegistry.cs     (7.3.3節：載入 pet_visuals.json、掃描資料夾建索引)
│   └── VisualFallbackResolver.cs (7.3.4節：缺圖 fallback 鏈)
├── Core/Sounds/
│   ├── IPetSoundSource.cs   (音效來源抽象介面，6.6.4節)
│   ├── SoundManager.cs      (音效匯入/試聽/切換/管理)
│   └── GlobalAudioManager.cs (背景音樂播放控制，全域單例，隨機選曲+靜音)
├── Core/Localization/
│   └── LocalizationManager.cs (12.2節：語言掃描/載入/切換/fallback)
└── Resources/
    ├── Assets/
    └── Localization/
        ├── zh-TW.json
        ├── en-US.json
        └── ja-JP.json
```

---

## 📞 15. 修改歷史

> 詳細差異見 git 歷史（13 節）。本表僅記錄各版的決策重點。

| 版本 | 日期 | 決策重點 |
|------|------|---------|
| 1.0 | 2026-08-27 | 初始版本 |
| 1.1 | 2026-08-27 | 雙寵物模式；自訂圖樣系統 |
| 1.2 | 2026-08-27 | 自訂音效系統；素材管理中心（6.6 節） |
| 1.3 | 2026-08-27 | 靜態圖片優先；雙寵物互動行為 |
| 1.4 | 2026-08-27 | 多語言（12 節）；互動素材規格；內建主題定為 2 套；不做裁切工具 |
| 1.5 | 2026-08-27 | 心情收斂為 3 分支；新增 7.3 動畫系統，確立「每類型皆可擴充多個單元」；圖樣改為資料夾制 |
| 1.6 | 2026-08-27 | 平台下修至 Win10；技術棧定案 WPF + .NET 8；新增 7.4 狀態數值規則；新增 13 節版本控制；修正 Hunger 方向性錯誤 |
| 1.7 | 2026-08-27 | 素材改為靜態圖與 Sprite Sheet 並存（6.4.5 統一動畫單元模型）；7.1 拆雙層計時器；7.3.5 責任邊界重劃 |
| 1.8 | 2026-08-27 | 精簡文件：移除已作廢的技術棧評估過程、inline 版本註記、歷史對照表。設計內容無變更 |
| 1.9 | 2026-08-27 | ① 健康度規則正式採用（7.4.5），脫離待決議；② 幸福度衰減放慢：改以小時為結算單位，放置不管由 100 降至 0 由原本 4 小時延長為 **24 小時**；③ **程式關閉時凍結 `Happiness` / `Health`**，離線只補算 `Hunger` / `Energy`（7.4.4）；冷落判定改用 `AwakeIdleSeconds` 累計秒數而非時間戳記，否則凍結會形同虛設；④ 新增 7.5 天氣連動：依所在地天氣在 `NEUTRAL` 時以 30% 機率改播 `anim_weather_*`，素材缺漏或查詢失敗一律靜默停用；預設關閉（涉及位置資訊） |
| 2.0 | 2026-08-29 | 離線行為簡化：程式關閉期間**四項數值全部凍結**（原本 `Hunger` / `Energy` 仍離線補算，改為一律不補算，`Happiness` / `Health` 續凍結）；7.4.4 由「離線補算」改為「離線凍結」，移除 24 小時上限、時鐘回調防護與 `ApplyOfflineDecay`（改為僅重設 `LastTickTime` 的 `ApplyOfflineFreeze`）；重開程式時寵物狀態與關閉當下完全相同 |

## ❓ 常見問題區域 (待記錄)

### 需要澄清的項目

- [ ] 寵物角色設計確定了嗎?（指實際角色造型；風格方向已定為暫不決定）
- [ ] 上線後的更新策略?（搭配 13.5 節的 GitHub Releases 流程一併考慮）
- [ ] Sprite Sheet 是否需支援**多列網格**排列？目前 6.4.3 節限定單列橫向，格數多時圖片會很寬（16 格 × 256px = 4096px），部分老舊顯示卡的材質尺寸上限為 4096px
- [ ] 6.5.2 節的互動素材（`interaction_*.png`）是否要開放 Sprite Sheet？已確認維持單張（因雙方抽圖不同步），但「單一單元做成連續動畫」不涉及隨機挑選，不受該理由限制
