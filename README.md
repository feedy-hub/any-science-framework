# Any Science Framework

<p align="center">
  <a href="./README.md">简体中文</a> |
  <a href="./README.zh-TW.md">繁體中文</a> |
  <a href="./README.en.md">English</a>
</p>

Any Science Framework 是一个面向科研工作的自举式 AI 助手框架。它不是单一领域的论文助手，而是一套可以先进行领域特化、再围绕想法、实验、审查、分析和迭代形成闭环的本地项目生成器。

这个仓库保存的是框架开发包。你可以用它生成一个新的 Any Science 科研工作区，也可以在这里继续开发框架本体、构建发布脚本、运行回归测试。

## 这个项目解决什么问题

很多 AI 科研助手容易在几个地方失控：

- 把领域背景、实验状态、分析结论混在对话里，跨会话后很难追踪。
- idea、实验设计、结果分析没有明确状态机，容易跳步、补叙、移动成功判据。
- reviewer 只是口头提醒，没有形成强制审查和可回归的文件契约。
- 实验数字没有结构化来源，结论可能无法追溯到结果文件。
- 脚本可以跑一次，但缺少构建、测试和发布流程，后续维护容易退化。

Any Science Framework 的目标是把这些约束变成项目结构、协议文件、hook、校验器和验收测试，而不是只靠提示词自觉。

## 核心特性

- 领域无关的科研闭环：`IDEA -> DESIGN -> APPROVED -> RUNNING -> ANALYZED -> ITERATE / PROMOTE / KILLED`
- 领域特化入口：新工作区默认要求先运行 `/build`，生成领域档案、领域 skill 和 reviewer 清单。
- 协议优先：`PROTOCOL.md` 作为文件格式、状态冲突和错误码的最高裁决依据。
- reviewer 誊写制：reviewer 只审不改，由总管把审查意见写入产出文件并更新最终 `REVIEW:` 行。
- 自动校验：生成的工作区内置 `scripts/validate.sh`，检查 status、REVIEW、graveyard 和 metrics schema。
- hook 门禁：生成的 `.claude/settings.json` 会配置 PostToolUse 和 Stop hook，拦截缺少审查状态或协议不合法的产出。
- 安全边界：默认拒绝读取密钥类文件，限制破坏性命令，并明确说明这些是软约束，不替代 Docker/devcontainer 隔离。
- 验收测试包：`setup_test.sh` 会生成 `test-kit/`，覆盖数据泄漏、伪造引用、移动球门、噪声内提升和坟场防复踩等场景。
- 可维护发布流程：本仓库用 `src/` 管理源脚本，用 `dist/` 发布可直接使用的脚本，并用 smoke test 防止 CRLF、语法和协议回归。

## 仓库结构

```text
any-science-framework/
├── src/
│   ├── setup.sh          # 源码版工作区生成脚本
│   └── setup_test.sh     # 源码版验收测试包生成脚本
├── dist/
│   ├── setup.sh          # 可直接使用的发布版工作区生成脚本
│   └── setup_test.sh     # 可直接使用的发布版测试包生成脚本
├── scripts/
│   └── build.sh          # 从 src 构建 dist，并强制 LF 行尾
├── tests/
│   └── smoke.sh          # 端到端回归测试
├── docs/
│   ├── design.md         # 当前设计范围
│   └── plan.md           # 实施计划和发布检查项
├── .gitattributes        # 强制脚本和文档使用 LF 行尾
└── README.md
```

## 快速开始

### 1. 克隆仓库

```bash
git clone https://github.com/feedy-hub/any-science-framework.git
cd any-science-framework
```

### 2. 生成一个新的科研工作区

建议显式指定一个新目录，避免覆盖已有工作区。

```bash
bash dist/setup.sh /tmp/my-any-science
cd /tmp/my-any-science
```

在 Windows + WSL 环境里，也可以生成到 Windows 磁盘：

```bash
bash dist/setup.sh /mnt/d/fu_files/工作/其他/my-any-science
cd /mnt/d/fu_files/工作/其他/my-any-science
```

### 3. 初始化提交

```bash
git add -A
git commit -m init
```

### 4. 进入 Claude 并进行领域特化

```bash
claude
```

进入会话后运行：

```text
/build
```

`/build` 会启动领域特化流程。未特化前，生成的总管会拒绝直接开展研究工作，这是设计行为。

### 5. 生成并运行验收测试包

在生成的科研工作区里运行：

```bash
bash /path/to/any-science-framework/dist/setup_test.sh
```

然后按 `test-kit/TESTS.md` 做验收。首次使用建议先跑其中的“附加自检”，确认 hook、校验器和权限规则在你的 Claude Code 版本下仍然生效。

## 生成的科研工作区包含什么

运行 `dist/setup.sh` 后，会得到一个完整的 Any Science 工作区：

```text
my-any-science/
├── CLAUDE.md                       # 科研总管角色说明
├── PROTOCOL.md                     # 文件契约、状态机、错误码和排错规则
├── README.md                       # 生成工作区的使用说明
├── .claude/
│   ├── agents/                     # builder、scholar、methodologist、executor、analyst、reviewer
│   ├── commands/                   # /build、/status、/spawn
│   ├── settings.json               # permissions 和 hooks
│   └── skills/                     # bootstrap、scientific-method、agent-factory、review-rubric
├── domain/
│   ├── PROFILE.md                  # 领域档案，初始为 TODO
│   ├── skills/                     # 领域特化 skill
│   └── references/                 # 领域参考资料
├── scripts/
│   ├── validate.sh                 # 协议校验器
│   ├── review_gate.sh              # PostToolUse 审查门禁
│   ├── pending_check.sh            # Stop hook 检查
│   ├── safe_kill.sh                # 长任务安全终止
│   ├── fork.sh                     # 长期并行研究线
│   └── harvest.sh                  # 并行线知识回收
├── templates/
│   ├── idea_card.md
│   └── exp_card.md
└── workspace/
    ├── ideas/
    ├── experiments/
    └── knowledge/
```

## 推荐工作流

### 领域特化

1. 进入生成的工作区。
2. 启动 `claude`。
3. 运行 `/build`。
4. 按 builder 的访谈问题提供领域、研究形态、资源、目标场所和 reviewer 红线。
5. builder 会生成或补全 `domain/PROFILE.md`、领域 skill 和 reviewer 定制清单。

### idea 到实验

1. 让 scholar 做领域调查、新颖性核查或 idea 发掘。
2. idea 卡片写入 `workspace/ideas/IDEA-<id>.md`。
3. reviewer 进行 L1 或 L2 审查。
4. 总管誊写 reviewer 意见，并把文件末尾 `REVIEW:` 改为 `PASS` 或 `REVISE`。
5. methodologist 把通过的 idea 设计成实验卡片。

### 实验执行与分析

1. executor 按实验卡片执行，不擅自改变成功判据。
2. 结果写入 `workspace/experiments/<id>/results/`。
3. `metrics.json` 必须满足 `PROTOCOL.md` 中的 schema。
4. analyst 先跑 `bash scripts/validate.sh`，再分析结果。
5. analyst 必须按事前成功判据给出 `ITERATE`、`PROMOTE` 或 `KILL` 建议。

### 状态巡检

```bash
bash scripts/validate.sh
```

或在 Claude 会话里运行：

```text
/status
```

## 开发本框架

如果你要修改 Any Science Framework 本体，而不是使用生成后的科研工作区，请在本仓库开发。

### 构建发布脚本

```bash
bash scripts/build.sh
```

构建脚本会：

- 从 `src/` 复制脚本到 `dist/`。
- 统一转换为 LF 行尾。
- 设置可执行权限。
- 运行 `bash -n` 做语法检查。

### 运行端到端测试

```bash
bash tests/smoke.sh
```

smoke test 会：

- 构建发布脚本。
- 生成临时 Any Science 工作区。
- 运行生成工作区内的 `scripts/validate.sh`。
- 生成 `test-kit/`。
- 构造缺少 REVIEW、重复 status、REVIEW 不在文件末尾、非法 metrics 等坏例，确认校验器会拒绝。

## 安全说明

Any Science Framework 会通过 `.claude/settings.json` 配置 deny/ask 权限规则，并通过 hook 和校验器减少误操作。但这些都是应用层软约束：

- 可以防止常见误读密钥、误删文件、误跳过审查。
- 不能对抗恶意代码或蓄意绕过。
- 不能替代 Docker、devcontainer、虚拟机或操作系统级权限隔离。

如果你要执行不可信来源的代码或处理敏感数据，请把生成的科研工作区放在隔离环境中运行。

## 常见问题

### 为什么新工作区一开始拒绝做研究？

因为 `domain/PROFILE.md` 初始是未特化状态。框架要求先运行 `/build`，明确领域、证据类型、资源、目标场所和审查标准，再开始研究。

### `dist/` 和 `src/` 有什么区别？

`src/` 是源码版本，适合开发者修改。`dist/` 是发布版本，适合直接使用。每次修改 `src/` 后都应该运行：

```bash
bash scripts/build.sh
```

### 会不会覆盖我已有的项目？

如果你把 `dist/setup.sh` 指向已有目录，它会在该目录中写入框架文件，可能和已有文件混在一起。建议始终指定一个新的空目录。

### 为什么要强制 LF 行尾？

这个项目主要通过 Bash 运行。Windows CRLF 行尾会导致 WSL/bash 把 `\r` 当成命令内容，出现 `set: -\r: invalid option` 或路径异常。仓库用 `.gitattributes` 强制 LF，构建脚本也会再次归一化。

## 路线图

- 将当前单文件 `setup.sh` 拆成模板目录，例如 `templates/agents/`、`templates/scripts/`、`templates/skills/`。
- 增加更细粒度的测试用例，覆盖 hook 输入 schema 漂移。
- 增加 release 打包脚本和版本号。
- 增加可选的 Docker/devcontainer 示例。
- 增加更多领域特化模板，例如 AI/ML、生物医学、材料、社会科学和理论研究。

## License

尚未指定许可证。公开使用前建议补充明确的开源许可证。

