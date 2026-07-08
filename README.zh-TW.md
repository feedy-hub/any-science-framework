# Any Science Framework

<p align="center">
  <a href="./README.md">简体中文</a> |
  <a href="./README.zh-TW.md">繁體中文</a> |
  <a href="./README.en.md">English</a>
</p>

Any Science Framework 是一個面向科研工作的自舉式 AI 助手框架。它不是單一領域的論文助手，而是一套可以先進行領域特化，再圍繞想法、實驗、審查、分析和迭代形成閉環的本地專案生成器。

這個倉庫保存的是框架開發包。你可以用它生成一個新的 Any Science 科研工作區，也可以在這裡繼續開發框架本體、構建發布腳本、執行回歸測試。

## 這個專案解決什麼問題

很多 AI 科研助手容易在幾個地方失控：

- 把領域背景、實驗狀態、分析結論混在對話裡，跨會話後很難追蹤。
- idea、實驗設計、結果分析沒有明確狀態機，容易跳步、補敘、移動成功判據。
- reviewer 只是口頭提醒，沒有形成強制審查和可回歸的文件契約。
- 實驗數字沒有結構化來源，結論可能無法追溯到結果文件。
- 腳本可以跑一次，但缺少構建、測試和發布流程，後續維護容易退化。

Any Science Framework 的目標是把這些約束變成專案結構、協議文件、hook、校驗器和驗收測試，而不是只靠提示詞自覺。

## 核心特性

- 領域無關的科研閉環：`IDEA -> DESIGN -> APPROVED -> RUNNING -> ANALYZED -> ITERATE / PROMOTE / KILLED`
- 領域特化入口：新工作區預設要求先執行 `/build`，生成領域檔案、領域 skill 和 reviewer 清單。
- 協議優先：`PROTOCOL.md` 作為文件格式、狀態衝突和錯誤碼的最高裁決依據。
- reviewer 謄寫制：reviewer 只審不改，由總管把審查意見寫入產出文件並更新最終 `REVIEW:` 行。
- 自動校驗：生成的工作區內建 `scripts/validate.sh`，檢查 status、REVIEW、graveyard 和 metrics schema。
- hook 門禁：生成的 `.claude/settings.json` 會配置 PostToolUse 和 Stop hook，攔截缺少審查狀態或協議不合法的產出。
- 安全邊界：預設拒絕讀取密鑰類文件，限制破壞性命令，並明確說明這些是軟約束，不替代 Docker/devcontainer 隔離。
- 驗收測試包：`setup_test.sh` 會生成 `test-kit/`，覆蓋資料洩漏、偽造引用、移動球門、噪聲內提升和墳場防復踩等場景。
- 可維護發布流程：本倉庫用 `src/` 管理源腳本，用 `dist/` 發布可直接使用的腳本，並用 smoke test 防止 CRLF、語法和協議回歸。

## 倉庫結構

```text
any-science-framework/
├── src/
│   ├── setup.sh          # 源碼版工作區生成腳本
│   └── setup_test.sh     # 源碼版驗收測試包生成腳本
├── dist/
│   ├── setup.sh          # 可直接使用的發布版工作區生成腳本
│   └── setup_test.sh     # 可直接使用的發布版測試包生成腳本
├── scripts/
│   └── build.sh          # 從 src 構建 dist，並強制 LF 行尾
├── tests/
│   └── smoke.sh          # 端到端回歸測試
├── docs/
│   ├── design.md         # 當前設計範圍
│   └── plan.md           # 實施計畫和發布檢查項
├── .gitattributes        # 強制腳本和文件使用 LF 行尾
└── README.md
```

## 快速開始

### 1. 複製倉庫

```bash
git clone https://github.com/feedy-hub/any-science-framework.git
cd any-science-framework
```

### 2. 生成新的科研工作區

建議明確指定一個新目錄，避免覆蓋已有工作區。

```bash
bash dist/setup.sh /tmp/my-any-science
cd /tmp/my-any-science
```

在 Windows + WSL 環境裡，也可以生成到 Windows 磁碟：

```bash
bash dist/setup.sh /mnt/d/fu_files/工作/其他/my-any-science
cd /mnt/d/fu_files/工作/其他/my-any-science
```

### 3. 初始化提交

```bash
git add -A
git commit -m init
```

### 4. 進入 Claude 並進行領域特化

```bash
claude
```

進入會話後執行：

```text
/build
```

`/build` 會啟動領域特化流程。未特化前，生成的總管會拒絕直接展開研究工作，這是設計行為。

### 5. 生成並執行驗收測試包

在生成的科研工作區裡執行：

```bash
bash /path/to/any-science-framework/dist/setup_test.sh
```

然後按照 `test-kit/TESTS.md` 做驗收。首次使用建議先跑其中的「附加自檢」，確認 hook、校驗器和權限規則在你的 Claude Code 版本下仍然生效。

## 生成的科研工作區包含什麼

執行 `dist/setup.sh` 後，會得到一個完整的 Any Science 工作區：

```text
my-any-science/
├── CLAUDE.md
├── PROTOCOL.md
├── README.md
├── .claude/
│   ├── agents/
│   ├── commands/
│   ├── settings.json
│   └── skills/
├── domain/
│   ├── PROFILE.md
│   ├── skills/
│   └── references/
├── scripts/
├── templates/
└── workspace/
```

其中 `PROTOCOL.md` 定義狀態機、文件契約和錯誤碼；`scripts/validate.sh` 負責協議校驗；`workspace/` 保存 idea、實驗和知識沉澱。

## 推薦工作流

1. 進入生成的工作區。
2. 啟動 `claude` 並執行 `/build`。
3. 讓 scholar 做文獻調查、新穎性核查或 idea 發掘。
4. 讓 methodologist 把通過審查的 idea 設計成實驗。
5. 讓 executor 執行實驗並把結果寫入 `results/`。
6. 讓 analyst 按事前成功判據分析結果，給出 `ITERATE`、`PROMOTE` 或 `KILL` 建議。
7. 用 `bash scripts/validate.sh` 或 `/status` 做狀態巡檢。

## 開發本框架

如果你要修改 Any Science Framework 本體，而不是使用生成後的科研工作區，請在本倉庫開發。

```bash
bash scripts/build.sh
bash tests/smoke.sh
```

`scripts/build.sh` 會從 `src/` 生成 `dist/`，統一 LF 行尾並做語法檢查。`tests/smoke.sh` 會端到端生成臨時工作區，並檢查常見壞例會被拒絕。

## 安全說明

Any Science Framework 透過 `.claude/settings.json`、hook 和校驗器減少誤操作。但這些是應用層軟約束，不能對抗惡意程式碼或蓄意繞過。若要執行不可信來源的程式碼或處理敏感資料，請使用 Docker、devcontainer、虛擬機或其他系統級隔離。

## 常見問題

### 為什麼新工作區一開始拒絕做研究？

因為 `domain/PROFILE.md` 初始是未特化狀態。框架要求先執行 `/build`，明確領域、證據類型、資源、目標場所以及審查標準。

### `dist/` 和 `src/` 有什麼區別？

`src/` 是源碼版本，適合開發者修改。`dist/` 是發布版本，適合直接使用。每次修改 `src/` 後都應該執行 `bash scripts/build.sh`。

### 會不會覆蓋我已有的專案？

如果你把 `dist/setup.sh` 指向已有目錄，它會在該目錄中寫入框架文件，可能和已有文件混在一起。建議始終指定新的空目錄。

## 路線圖

- 將目前單文件 `setup.sh` 拆成模板目錄。
- 增加更多 hook schema 漂移測試。
- 增加 release 打包腳本和版本號。
- 增加 Docker/devcontainer 示例。
- 增加更多領域特化模板。

## License

尚未指定授權。公開使用前建議補充明確的開源授權。

