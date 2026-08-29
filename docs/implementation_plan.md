# 實作工作計畫（Phase 1 MVP）

本檔把設計檔第 9 節（開發進度）與第 14 節（程式碼結構）拆成**「一個 session 一項」的小工作單元**，供逐項開新 session 實作。權威來源仍是 `docs/desktop_pet_design_doc.md`，本檔只是任務切分與相依索引。

## 使用方式

- **每項開一個 `feature/*` 分支**，commit 訊息帶章節編號（例：`feat(7.4): 實作幸福度衰減與冷卻機制`）。
- 每個新 session 建議先讀 `CLAUDE.md` + 該項對應的設計檔章節，再動手。
- **WPF / .NET 8 無法在此 Linux 容器建置或執行**，每項產出程式碼後需在 Windows 環境驗證。
- 大小標記：**S**＝小（單一 session 輕鬆完成）、**M**＝中（單一 session 可完成，勿再塞更多）。
- 完成一項就把下方對應核取框打勾，並在 commit 一併更新本檔。

## 群組 A — 基礎骨架（先做，序列相依）

- [x] **A1 專案骨架** — 參照 §4/§13.1/§13.2/§14 — `.sln`、`src/DesktopPet/DesktopPet.csproj`（`net8.0-windows`/`UseWPF`/單檔發佈屬性）、`App.xaml(.cs)`、§14 資料夾結構、`.gitignore`、`README.md`。相依：無。**M**
- [x] **A2 資料模型** — 參照 §5.1/§7.4.6/§12.4 — `Models/`：`Pet`（含 `PetSoundSet`）、`SkinInfo`、`PetMood`、`GameState`、`Settings`（含 `GlobalAudioSettings`/`CurrentLanguage`）。相依：A1。**S**
- [x] **A3 JSON 序列化 + 儲存** — 參照 §4/§5.2/§8 — `Utils/StorageManager.cs`：讀寫存檔、註冊 `JsonStringEnumConverter` + 命名策略（`PetMood.LowEnergy` → `"LOW_ENERGY"`）、5 分自動保存掛點、保留 3 份備份。相依：A2。**M**

## 群組 B — 動畫 / 視覺核心

- [x] **B1 心情判定 + 狀態列舉** — 參照 §7.2.1/§7.3 — `Core/Visuals/PetVisualState.cs`（6 態）、`MoodEvaluator.cs`（三分支，順序不可換：Hunger>70→SAD、Energy<20→LOW_ENERGY、否則 NEUTRAL）。相依：A1。**S**
- [ ] **B2 素材抽象介面 + skin.json** — 參照 §6.4.4/§6.4.5 — `Core/Skins/`：`FrameRef`、`IPetSkinSource`、`SkinManifest`（無 `skin.json` 時全視為 `frames:1`）、`VisualUnitInfo`。相依：A1。**M**
- [ ] **B3 素材來源實作** — 參照 §6.4.4/§7.3.5/§7.3.6 — `StaticImageSkinSource`、`SpriteSheetSkinSource`（依 `elapsed` 推格、`loop`/停最後一格）、延遲載入 + LRU（總計 48 格/隻）。相依：B2。**M**
- [ ] **B4 視覺登記 + fallback** — 參照 §7.3.3/§7.3.4 — `Core/Visuals/VisualRegistry.cs`（載入 `pet_visuals.json`、掃描資料夾建索引）、`VisualFallbackResolver.cs`（缺圖 fallback 鏈）。相依：B1。**M**
- [ ] **B5 單元選擇器** — 參照 §7.3.5 — `PetVisualSelector.cs`：`ResolveUnit`（狀態改變或 `rerollIntervalSec` 到才重抽、避免連續抽到同一個、`_unitStartTime`/`ElapsedInUnit`）。相依：B4。**M**
- [ ] **B6 雙層計時器 + 動態渲染頻率** — 參照 §7.1/§7.1.1 — `RenderTickController.cs`（依當前單元格數 1–15 Hz、單格暫停重繪、多格升至該單元 fps）、1 Hz 狀態 tick 骨架。相依：B3、B5。**M**
- [ ] **B7 佔位素材 + 設定檔** — 參照 §7.3.3/§11.1 — `pet_visuals.json`、`interaction_types.json`、2 套內建主題資料夾（色塊佔位 `anim_idle_1` 等 + `skin.json`），讓 MVP 可實跑。相依：B4。**S–M**

## 群組 C — 狀態系統

- [ ] **C1 數值變化 + 健康度 + 離線凍結** — 參照 §7.4.1/§7.4.4/§7.4.5 — `Core/StateManager.cs`（1 Hz：Hunger 每 3 分 +1、Energy 每 5 分 -1、`AwakeIdleSeconds`/`HealthCheckSeconds` 累加）、健康度每 30 分結算、`OfflineFreezeHandler.cs`（**四項全凍結、僅重設 `LastTickTime`**）。相依：A2。**M**
- [ ] **C2 幸福度衰減與回補** — 參照 §7.4.2/§7.4.3 — `Core/HappinessManager.cs`（疊加衰減、回補 + 冷卻；冷卻期照常操作，只不加幸福度）。相依：C1。**M**

## 群組 D — 視窗 / 輸入 / 渲染

- [ ] **D1 透明置頂視窗** — 參照 §6.1/§10.2/§10.3 — `MainWindow.xaml(.cs)`：`AllowsTransparency`、`WindowStyle=None`、`Topmost`、`WS_EX_LAYERED`、DPI（Per-Monitor V2）、防最小化消失、不遮工作列。相依：A1。**M**
- [ ] **D2 點穿模式** — 參照 §2.1/§10.2 — P/Invoke 設定 `WS_EX_TRANSPARENT` 切換，連動 `clickThrough` 設定。相依：D1。**S**
- [ ] **D3 輸入 + 右鍵選單** — 參照 §6.3/§7.3.2 — 點擊/拖曳/雙擊、右鍵選單（餵食/玩耍/睡眠/清潔/設置/關於/退出）、觸發 CLICK/FEED/SLEEP 事件。相依：D1。**M**
- [ ] **D4 渲染綁定** — 參照 §6.4.4/§7.3.2 — 把 `FrameRef` 以 `ImageBrush`/`CroppedBitmap` 畫到視窗；事件圖 > 心情圖、「至少 N 秒」（`max(durationSec, frames/fps)`）、進行中不被打斷。相依：B6、D3。**M**

## 群組 E — 整合 / 多寵物

- [ ] **E1 PetInstance 整合** — 參照 §3/§14 — `Core/PetInstance.cs`：把單一寵物的 視窗 + 狀態 + 視覺 + 輸入 串成一個運行單元。相依：C2、D4。**M**
- [ ] **E2 Coordinator + Onboarding** — 參照 §3.1/§6.5.1 — `Core/PetCoordinator.cs`（管理 1–2 隻、單寵物略過互動）、`UI/OnboardingWindow.xaml`（首次選 1/2 隻）。相依：E1。**M**
- [ ] **E3 互動系統** — 參照 §6.5.2–§6.5.4 — `PetInteractionChecker`（交集判定）、`InteractionRules`（距離/觸發條件）、播 `interaction_*.png`（固定單張）。相依：E2。**M**
- [ ] **E4 存讀整合 + 自動保存** — 參照 §7.1/§8.2 — 串接 `StorageManager` 到 Coordinator/Instance：啟動載入 → 離線凍結 → 執行；5 分自動保存、關閉前保存；右鍵動作實際改數值。相依：C2、E2。**M**

> **Phase 1 到此為 MVP 可跑版**：單/雙寵物、動畫、狀態、存檔、右鍵選單。

## 相依與並行建議

- 起手序列：**A1 → A2 → A3**。之後 **B 群** 與 **C 群** 可並行推進（不同 session／不同人）。
- **B7 佔位素材建議早做**（B4 之後即可），讓 D4 渲染有實際素材可顯示。
- 收斂點：**E1** 需要 C2 + D4 都完成；**E4** 需要 C2 + E2。

## Phase 2 / 3（待 Phase 1 收尾前再依相同顆粒度細拆）

- **Phase 2**：音效系統（§6.6）、天氣連動（§7.5）、多語言（§12）、系統托盤、進度統計（飼養天數/等級）、更豐富素材與 Sprite Sheet 混用驗證。
- **Phase 3（可選）**：小遊戲、進化/升級、成就解鎖、社交分享。
