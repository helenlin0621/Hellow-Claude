# DesktopPet — Windows 桌面寵物

在桌面上飼養 1–2 隻虛擬寵物的 Windows 應用：透明視窗、可拖曳、可點穿、始終置頂，具備動畫、狀態養成、多寵物互動、自訂素材與多語言。

> **目前狀態**：Phase 1（MVP）開發中。本 commit 建立專案骨架（任務 **A1**），尚未有可運行的功能。
> 權威設計來源為 [`docs/desktop_pet_design_doc.md`](docs/desktop_pet_design_doc.md)；任務切分見 [`docs/implementation_plan.md`](docs/implementation_plan.md)。

## 技術棧

- **C# WPF + .NET 8**（`net8.0-windows`、`UseWPF`）
- 發佈：單檔自包含 `win-x64`（`PublishSingleFile` + `SelfContained`）
- 目標平台：Windows 10 (1809) 以上，x64 / Arm64（WPF 為 Windows 專屬，無跨平台路徑）
- 依賴：`System.Text.Json`（.NET 內建）、`System.Drawing.Common`、可選 `NAudio`（音效，後續任務）

## 建置與執行

> ⚠️ **WPF / `net8.0-windows` 只能在 Windows 環境建置與執行。** 本專案的開發容器為 Linux，無法在此建置或執行，需在 Windows 上驗證。

```bash
# 建置
dotnet build -c Release

# 執行（開發）
dotnet run --project src/DesktopPet

# 發佈單檔 exe（設計檔 §13.5）
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## 儲存庫結構

```
DesktopPet/
├── DesktopPet.sln
├── .gitignore
├── README.md
├── docs/
│   ├── desktop_pet_design_doc.md   ← 設計檔（唯一權威來源）
│   └── implementation_plan.md      ← Phase 1 任務切分
└── src/DesktopPet/                 ← 程式碼（見設計檔 §14）
    ├── DesktopPet.csproj
    ├── App.xaml(.cs)               ← 應用程式進入點
    ├── Core/                       ← 協調器、寵物實例、狀態管理
    │   ├── Interaction/            ← 多寵物互動判定
    │   ├── Skins/                  ← 素材抽象（FrameRef / IPetSkinSource / skin.json）
    │   ├── Visuals/                ← 心情判定、單元選擇、渲染節奏
    │   ├── Weather/                ← 天氣連動
    │   ├── Sounds/                 ← 音效
    │   └── Localization/           ← 多語言
    ├── UI/                         ← 設定 / Onboarding / 素材管理 / 狀態面板
    ├── Models/                     ← 資料模型（Pet / GameState / Settings）
    ├── Utils/                      ← StorageManager 等工具
    └── Resources/
        ├── Assets/                 ← 內建素材
        └── Localization/           ← 語言檔（zh-TW / en-US / ja-JP）
```

> 目前 `src/DesktopPet/` 下的子資料夾多為以 `.gitkeep` 佔位的空目錄，實際檔案由後續任務（A2 起）逐項填入。

## 使用者資料位置

執行期使用者資料存於 `%APPDATA%\DesktopPet\`（`pet_data.json`、`settings.json`、`custom_skins/`、`custom_sounds/` 等），**位於儲存庫之外**（設計檔 §8.1）。匯入的圖片／音效會**複製**到專屬資料夾，不直接引用原檔路徑。這些檔案已列入 `.gitignore`，絕不進版控。

## 開發約定

- 依 [`docs/implementation_plan.md`](docs/implementation_plan.md) 一項一分支（`feature/*` / `fix/*`）。
- 提交訊息帶設計檔章節編號，例：`feat(7.4): 實作幸福度衰減與冷卻機制`。
- `main` 保持可建置；設計檔隨程式碼一起版控，讓設計決策與實作變更落在同一 commit。

## 授權

待定。
