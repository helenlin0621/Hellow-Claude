# CLAUDE.md

本檔為 Claude Code 在此儲存庫工作時的指引。**唯一權威來源是 `docs/desktop_pet_design_doc.md`（設計檔）**；本檔僅摘錄關鍵決策與最容易踩雷的不變量，遇到衝突或缺漏時一律以設計檔為準。

## 專案概述

**Windows 桌面寵物系統**：在桌面上飼養 1–2 隻虛擬寵物的應用。透明視窗、可拖曳、可點穿、始終置頂，具備動畫、狀態養成、多寵物互動、自訂素材與多語言。

- **目前狀態**：規劃階段（設計檔 v2.0），**尚無程式碼**。第一步是 Phase 1 (MVP)。
- **開發優先級**：Phase 1 基礎顯示/動畫/交互 → Phase 2 狀態/存檔/音效/天氣/多語言 → Phase 3 遊戲/進化/成就（可選）。
- **實作任務切分見 `docs/implementation_plan.md`**（一項一 session、含相依與大小）。每開新 session 先讀此檔決定要做哪一項。

## 技術棧

- **C# WPF + .NET 8**，`TargetFramework = net8.0-windows`，`UseWPF = true`
- 發佈：單檔自包含 `win-x64`（`PublishSingleFile` + `SelfContained`）
- 目標平台：Windows 10 (1809) 以上，x64 / Arm64（WPF 為 Windows 專屬，無跨平台路徑）
- 依賴：`System.Text.Json`（內建）、`System.Drawing.Common`、可選 `NAudio`（音效）

## 常用命令

```bash
# 建置
dotnet build -c Release

# 執行（開發）
dotnet run --project src/DesktopPet

# 發佈單檔 exe（見設計檔 13.5）
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

> 注意：本開發容器為 Linux，`net8.0-windows` / WPF **無法在此建置或執行**，需在 Windows 環境驗證。

## 架構總覽

```
Main App → Pet Coordinator (管理 1–2 隻 + 跨寵物互動判定)
             → Pet Instance #1 / #2 (各自獨立視窗/狀態/圖樣/動畫循環)
                  → Pet Logic / State Manager / Render Engine / Input Manager
             → Storage (JSON 持久化)
```

- 每隻寵物是獨立的 **Pet Instance**（獨立透明視窗）。單寵物模式時不建立 #2，Coordinator 自動略過互動檢查。
- 規劃中的程式碼結構見設計檔第 14 節（`Core/`、`Core/Skins/`、`Core/Visuals/`、`Core/Sounds/`、`Core/Weather/`、`Core/Localization/`、`UI/`、`Models/`、`Utils/`）。

## 核心不變量（最容易踩雷，務必遵守）

這些是設計檔反覆強調、寫錯會導致難以察覺的 bug 的規則：

### 狀態數值（第 7.4 節）
- **`Hunger` 飢餓度是「越高越餓」，隨時間遞增（每 3 分鐘 +1）。** 寫成遞減會讓 `Hunger > 70` 的 `SAD` 分支永遠觸發不到。
- **程式關閉時四項數值全部凍結**（`Hunger` / `Energy` / `Happiness` / `Health` 皆不變），不做任何離線補算。啟動時僅將 `LastTickTime` 重設為現在時刻；因離線不累計任何變化，改系統時鐘也無從刷數值。
- **冷落懲罰用 `AwakeIdleSeconds` 累計秒數判定，不可用 `LastInteractionTime` 與現在時刻的差值。** 否則凍結形同虛設（關機三天重開會瞬間狂扣幸福度）。
- **冷卻期間照常可操作**（動畫照播、音效照響、`AwakeIdleSeconds` 照歸零），只是不再累加幸福度。不可實作成「冷卻中禁止餵食/點擊」。
- 所有數值夾在 `0 ~ 100`。

### 心情與外觀（第 7.2.1 節）
- **心情判定只看 `Hunger` 與 `Energy`，完全不看 `Happiness`。** 判定順序不可調換：先 `Hunger > 70 → SAD`，再 `Energy < 20 → LOW_ENERGY`，否則 `NEUTRAL`。
- **`Happiness` 是純數值指標，不影響外觀。** 面板顯示 `😊 Mood` = Happiness，但寵物圖片與它解耦。

### 動畫（第 6.4.5 / 7.1 / 7.3 節）
- **統一動畫單元模型：靜態圖 = `frames` 為 1 的 Sprite Sheet。** 全系統只有一套邏輯，禁止散落 `if (isSpriteSheet)` 分支。
- **責任邊界**：`PetVisualSelector` 決定「播哪個單元」；`IPetSkinSource` 依 `elapsed` 決定「播第幾格」。兩者不可混在一起（否則會把 Sprite Sheet 凍結在第一格）。
- **抽籤只在「單元切換時機」發生**（狀態改變，或 `rerollIntervalSec` 到），不是每次 tick 重抽（1 Hz 每秒換圖看起來像故障）。多單元時避免連續抽到同一個。
- **雙層計時器**：狀態 tick 固定 1 Hz；渲染 tick 動態 1–15 Hz。單格單元暫停重繪，多格才升到該單元 fps（12–15 fps，**不用 30 fps**，因 `AllowsTransparency=True` 走軟體算圖是效能瓶頸）。
- **不以圖片尺寸推導 Sprite Sheet 格數**，格數必須由使用者明確指定（見 6.4.2.1）。Sprite Sheet 一律單列橫向。
- **缺 `skin.json` 的資料夾一律視為所有單元 `frames: 1`**（既有素材零遷移）。使用者永遠不需手寫 `skin.json`，由匯入流程自動維護。
- **`FrameRef`（底圖 + 矩形）而非每格新建 `BitmapImage`**，避免播放時每秒產生 12–15 個短命物件的 GC 壓力。
- 事件圖 > 心情圖優先。持續時間語意為「至少 N 秒」：`max(durationSec, frames/fps)`。事件進行中不被打斷（如 FEED 播放期間忽略新 CLICK）。

### JSON 序列化（第 4 節）
- **列舉存檔用字串，需註冊 `JsonStringEnumConverter` + 自訂命名策略**，使 `PetMood.LowEnergy` → `"LOW_ENERGY"`。預設會序列化為數字，命名對不上會導致舊存檔讀不回來。

### 資料驅動（不需改程式碼即可擴充）
- 圖片類型（`pet_visuals.json`）、互動類型（`interaction_types.json`）、語言檔（`Resources/Localization/*.json`）、圖樣資料夾內新增 `anim_*.png` — **全部純資料驅動，新增不需重編譯**。
- 心情代號與檔名前綴**不是一對一**（`LOW_ENERGY` → `anim_tired`），必須查 `pet_visuals.json`，不可用列舉名稱轉小寫推導。

### 多寵物與互動（第 6.5 節）
- **漸進式增強**：缺互動素材時各自獨立行動，不報錯、不卡住。
- 互動素材 `interaction_*.png` **固定單張靜態圖**（不比照 7.3 開放多張隨機），避免雙方各自抽圖不同步。觸發門檻是雙方互動類型的**交集**非空。

## Windows 特定注意事項

- **點穿模式無 WPF 內建 API**，需 P/Invoke 設定 `WS_EX_TRANSPARENT`（設計檔 10.2）。
- 透明視窗用 `WS_EX_LAYERED`；防最小化消失、多監視器、DPI 感知（Per-Monitor V2）、避免遮擋工作列。
- 記憶體控制在 50–100 MB；延遲載入（僅掃描檔名建索引，首次抽中才解碼），LRU 快取上限**總計 48 格**/隻。

## 存儲位置

- 執行期使用者資料在 `%APPDATA%\DesktopPet\`（`pet_data.json`、`settings.json`、`custom_skins/`、`custom_sounds/` 等），**在儲存庫之外**。
- 匯入的圖片/音效會**複製**到專屬資料夾（`custom_skins/{skinId}/`、`custom_sounds/`），不直接引用原檔路徑。

## Git 慣例（第 13 節）

- `main` 保持可建置。功能開 `feature/*`、修正開 `fix/*`，依第 9 節核取項目一項一分支。
- **提交訊息帶章節編號**，例如 `feat(7.4): 實作幸福度衰減與冷卻機制`，方便日後對照設計書。小步提交，一次只做一件事。
- 設計檔隨程式碼版控（`docs/desktop_pet_design_doc.md`），讓設計決策與實作變更落在同一 commit。
- **絕不進版控**：`custom_skins/`、`custom_sounds/`、`pet_data.json`、`settings.json`、`achievements.json`（使用者資料 / 測試存檔）。標準 .NET 忽略項（`bin/`、`obj/`、`.vs/`、`*.user`）亦然。
- 二進位素材：內建官方主題 PNG 與預設音效**進版控**（總量小）；PSD/AI 原始檔建議雲端或 Git LFS；使用者自訂素材不進版控。

## 儲存庫結構（目標，第 13.1 節）

```
DesktopPet/
├── .gitignore
├── README.md
├── docs/desktop_pet_design_doc.md   ← 設計檔（權威來源）
├── src/DesktopPet/                  ← 程式碼（見設計檔第 14 節）
└── assets-src/                      ← 素材原始檔（可選）
```
