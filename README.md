# Any Science Framework

<p align="center">
  <a href="./README.md">简体中文</a> |
  <a href="./README.zh-TW.md">繁體中文</a> |
  <a href="./README.en.md">English</a>
</p>

> From vague ideas to reviewed experiments, structured metrics, and traceable research decisions.

**Any Science Framework** 是一个面向 AI 辅助科研的、协议驱动的本地科研工作区生成器。

它不是一个单纯的 prompt 集，也不是某一个具体学科的论文助手，而是一套可以先进行领域特化、再围绕 **idea、实验设计、执行、审查、分析、复盘与迭代** 形成闭环的科研工作系统。

这个仓库保存的是框架开发包。你可以用它生成一个新的 Any Science 科研工作区，也可以继续开发框架本体、构建发布脚本、运行回归测试。

---

## 目录

- [这个项目解决什么问题](#这个项目解决什么问题)
- [核心思想](#核心思想)
- [它如何工作](#它如何工作)
- [生成的科研工作区包含什么](#生成的科研工作区包含什么)
- [快速开始](#快速开始)
- [推荐工作流](#推荐工作流)
- [内置科研 Agent](#内置科研-agent)
- [协议、审查与校验门禁](#协议审查与校验门禁)
- [开发本框架](#开发本框架)
- [适用场景](#适用场景)
- [与 Claude Code / Claude Science 的关系](#与-claude-code--claude-science-的关系)
- [安全边界](#安全边界)
- [常见问题](#常见问题)
- [路线图](#路线图)
- [License](#license)

---

## 这个项目解决什么问题

AI 科研助手很强，但当科研过程长期存在于聊天记录里时，很容易出现失控和不可追踪的问题：

- 领域背景、实验状态、分析结论混在对话里，跨会话后很难恢复上下文。
- idea、实验设计、结果分析没有明确状态机，容易跳步、补叙或移动成功判据。
- reviewer 只是口头提醒，没有形成强制审查和可回归的文件契约。
- 实验数字没有结构化来源，结论可能无法追溯到结果文件。
- 失败 idea 被遗忘，后续容易重复踩坑。
- 脚本可以跑一次，但缺少构建、测试和发布流程，后续维护容易退化。

**Any Science Framework 的目标是把 AI 辅助科研从“聊天式建议”升级为“有状态、有协议、有审查、有证据链的本地科研工作系统”。**

它不试图取代研究者，而是提供一个受约束的科研工作区，让 AI Agent 必须通过明确的文件、协议、角色和校验规则来推进科研任务。

---

## 核心思想

Any Science Framework 把科研过程建模为一个可追踪的状态机：

```text
IDEA → DESIGN → APPROVED → RUNNING → ANALYZED → ITERATE / PROMOTE / KILLED
```

每个状态都对应明确的文件、审查规则、指标 schema 和后续决策。

框架遵循三条核心原则：

### 1. Research is stateful

科研不是一次性问答。每个 idea、实验、结果、失败原因和推进决策都应该有明确状态。

### 2. Protocols over prompts

prompt 很有用，但长期科研工作不能只靠模型自觉。框架通过 `PROTOCOL.md`、文件格式、review gate、metrics schema 和 validation scripts 约束 Agent 行为。

### 3. Review before promotion

实验结果不能直接变成结论。任何 idea、实验设计或分析结论都需要经过 reviewer 审查，并由总管誊写审查意见后才能进入下一状态。

---

## 它如何工作

```mermaid
flowchart LR
    A[User Research Goal] --> B[/build Domain Specialization]
    B --> C[Generated Research Workspace]

    C --> D[CLAUDE.md<br/>Research PI]
    C --> E[PROTOCOL.md<br/>Workflow Contract]
    C --> F[Agents<br/>Scholar / Methodologist / Executor / Analyst / Reviewer]
    C --> G[Commands & Skills]
    C --> H[Scripts & Hooks]

    F --> I[Ideas]
    I --> J[Experiment Design]
    J --> K[Approved Experiment]
    K --> L[Execution]
    L --> M[Metrics & Results]
    M --> N[Analysis]
    N --> O{Decision}

    O --> P[Iterate]
    O --> Q[Promote]
    O --> R[Archive]
```

运行 `dist/setup.sh` 后，框架会生成一个新的科研工作区。这个工作区包含：

- 一个科研总管角色：`CLAUDE.md`
- 一个最高协议文件：`PROTOCOL.md`
- 一组角色化 Agent：scholar、methodologist、executor、analyst、reviewer 等
- 一组命令和 skill：`/build`、`/status`、`/spawn` 等
- 一组校验和 hook 脚本：`validate.sh`、`review_gate.sh`、`pending_check.sh` 等
- 一组科研资产目录：ideas、experiments、knowledge、reports 等

新工作区默认要求先运行 `/build`。`/build` 会通过访谈方式建立领域档案、研究约束、reviewer 红线和领域 skill。未完成领域特化前，总管会拒绝直接开展研究工作，这是设计行为。

---

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
│   ├── fork.sh                     # 长期并行研究线
│   └── harvest.sh                  # 并行线知识回收
├── templates/
│   ├── idea_card.md
│   └── exp_card.md
└── workspace/
    ├── ideas/                      # idea 卡片
    ├── experiments/                # 实验卡片、配置、日志、结果
    └── knowledge/                  # 研究记忆、graveyard、报告沉淀
```

---

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

---

## 推荐工作流

### 领域特化

1. 进入生成的工作区。
2. 启动 `claude`。
3. 运行 `/build`。
4. 按 builder 的访谈问题提供领域、研究形态、资源、目标场所和 reviewer 红线。
5. builder 会生成或补全 `domain/PROFILE.md`、领域 skill 和 reviewer 定制清单。

### Idea 到实验

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

---

## 内置科研 Agent

Any Science Framework 使用角色化 Agent，而不是让一个泛化助手负责所有科研环节。

| Agent | 职责 |
|---|---|
| PI / 总管 | 调度研究方向、维护状态机、汇总 reviewer 意见、决定下一步 |
| Builder | 通过 `/build` 建立领域档案、领域 skill 和 reviewer 清单 |
| Scholar | 文献调查、idea 发掘、新颖性核查 |
| Methodologist | 将 idea 转化为可执行实验设计，明确 MVE、对照、指标和成功判据 |
| Executor | 执行实验、写代码、跑流程、整理日志和结果 |
| Analyst | 分析实验结果、诊断失败原因、提出 ITERATE / PROMOTE / KILL 建议 |
| Reviewer | 只审不改，检查逻辑、证据、指标、引用和结论边界 |

Reviewer 的设计是这个框架的重要约束：

> reviewer 不直接修改产出文件，只提供审查意见；总管负责誊写意见并更新最终 `REVIEW:` 状态。

这样可以避免“自己写、自己审、自己通过”的隐性失控。

---

## 协议、审查与校验门禁

生成的工作区由 `PROTOCOL.md` 统管。

`PROTOCOL.md` 定义：

- 合法研究状态
- 文件头部和尾部格式
- `STATUS:` 与 `REVIEW:` 的兼容矩阵
- reviewer 誊写规则
- graveyard 条目格式
- `metrics.json` schema
- hook 输入输出契约
- 错误码和冲突处理方式

生成工作区内置校验器：

```bash
bash scripts/validate.sh
```

校验器会检查 status、REVIEW、graveyard 和 metrics schema。`.claude/settings.json` 还会配置 PostToolUse 与 Stop hook，用于拦截缺少审查状态或协议不合法的产出。

这套机制的目标是让科研过程可审计、可回归，并防止 Agent 悄悄跳过审查或报告非法结果。

---

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

### 仓库结构

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

---

## 适用场景

Any Science Framework 适合：

- AI 辅助科研工作流
- 机器学习实验管理
- 论文 idea 探索与筛选
- 可复现实验闭环
- 文献到实验的转化流程
- 多 Agent 科研助手
- 领域专属科研 Copilot
- 需要长期追踪 idea、实验、失败原因和结论边界的研究项目

它尤其适合那些实验迭代频繁、指标驱动明显、跨会话容易丢失上下文的科研方向。

---

## 与 Claude Code / Claude Science 的关系

Any Science Framework 不是 Anthropic 官方产品。

它是一个第三方开源框架，用来生成兼容 Claude Code 风格的本地科研工作区。你可以把它理解为一个轻量、文件化、协议驱动的科研操作层。

两者关系可以这样理解：

```text
Claude 模型
   ↓
Claude Code / Claude Science 运行环境
   ↓
Claude Science：官方科学工作台产品
Any Science Framework：第三方开源科研工作区生成器
   ↓
你的具体科研项目
```

Claude Science 更像产品级科学工作台，强调官方 UI、科学工具、数据分析和计算环境整合。

Any Science Framework 更像开源可改造的科研协议层，强调文件契约、多 Agent 分工、review gate、metrics schema 和实验状态机。它没有官方 Claude Science 的完整 UI，但更容易按自己的研究方向和工作习惯进行定制。

---

## 安全边界

Any Science Framework 会通过 `.claude/settings.json` 配置权限规则，并通过 hook 和校验器减少误操作。但这些都是应用层约束，不能替代容器、虚拟机或操作系统级隔离。

涉及重要数据、私有代码或远程计算资源时，建议在隔离环境中使用，并在执行前审查生成的命令、脚本和配置。

---

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

这个项目主要通过 Bash 运行。Windows CRLF 行尾可能导致脚本解析异常。仓库用 `.gitattributes` 强制 LF，构建脚本也会再次归一化。

### 这个框架能保证科研结论正确吗？

不能。它只能让过程更结构化、更可审查、更可追踪。科学判断仍然需要研究者负责，尤其是实验设计、统计有效性、领域假设和论文结论边界。

---

## 路线图

- [ ] 将当前单文件 `setup.sh` 拆成模板目录，例如 `templates/agents/`、`templates/scripts/`、`templates/skills/`。
- [ ] 增加更细粒度的测试用例，覆盖 hook 输入 schema 漂移。
- [ ] 增加 release 打包脚本和版本号。
- [ ] 增加可选的 Docker/devcontainer 示例。
- [ ] 增加更多领域特化模板，例如 AI/ML、生物医学、材料、社会科学和理论研究。
- [ ] 增加示例科研工作区。
- [ ] 增加英文 README 与多语言文档同步检查。

---

## License

尚未指定许可证。公开使用前建议补充明确的开源许可证。
