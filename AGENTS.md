# AGENTS.md - LocalMultiControl Mod 协作规范

本文件面向自动化编码代理，目标是让代理在本项目中稳定改动、可验证、可回滚。

## 0. 作用域与硬性约束

- 仅允许修改 `src/Mods/LocalMultiControl/`。
- `src/Core/`、`src/gdscript/`、`src/GameInfo/` 视为只读参考源码。
- 不要删除用户已有改动，不要用 `git reset --hard`、`git checkout --`。
- 语言：与用户沟通、日志说明、注释均用中文；标识符保持英文。
- 提交策略：每次会话完成代码修改后，立即提交一次 commit（除非用户明确要求不提交）。

## 1. 项目背景（必须理解）

- 项目名称：`LocalMultiControl`。
- 目标方向：实现"联机转本地多控"能力，把网络控制路径适配为单机多角色/多输入控制。
- 当前阶段：仅有工程骨架，**当前未引用 baselib 依赖**。
- 设计原则：先保证输入路由与状态同步安全，再逐步增加玩法逻辑。

## 2. 相关知识

知识库位于 `../../知识库/Mod 开发指南/`，包含从 [SlayTheSpire2ModdingTutorials](https://github.com/GlitchedReme/SlayTheSpire2ModdingTutorials) 克隆的 Mod 开发教程：
- `Basics/` - 基础开发知识
- `Visuals/` - 视觉相关开发
- `images/` - 教程配图
- `README.md` - 教程索引

如果需要使用 baselib 相关知识，可参考上述教程。**若使用了 baselib，必须在 README.md 中说明当前是否使用。**

## 3. 代码位置

- 入口：`Scripts/Entry.cs`
- 补丁目录：`Scripts/Patch/`
- 当前占位补丁：`Scripts/Patch/LocalMultiControlPatch.cs`

## 4. 工具使用规范（代理执行）

### 4.1 文件与代码操作

- 查找文件优先使用模式搜索工具（如 glob）。
- 内容检索优先使用正则搜索工具（如 grep）。
- 读取文件优先使用逐文件读取工具（如 read）。
- 小范围修改优先使用补丁方式（如 apply_patch）。

### 4.2 终端命令

- 仅在需要运行构建、格式化、git、脚本时使用终端命令。
- 命令执行目录固定在 `src/Mods/LocalMultiControl`，避免跨目录误操作。
- 涉及空格路径时必须使用引号。

### 4.3 Git 安全

- 禁止破坏性操作（`reset --hard`、强推等）。
- 不改全局 git config。
- 未经用户要求，不推送远程仓库。

## 5. Build / Lint / Test 命令

在目录 `src/Mods/LocalMultiControl` 下执行。

### 5.1 恢复与构建

- 依赖恢复：`dotnet restore LocalMultiControl.csproj`
- Debug 构建：`dotnet build LocalMultiControl.csproj -c Debug`
- Release 构建：`dotnet build LocalMultiControl.csproj -c Release`

### 5.1.1 构建产物路径约束（新增）

- `Godot.NET.Sdk` 默认会先把编译产物输出到：`.godot/mono/temp/bin/<Config>/DualRoleAdventure.dll`。
- 项目通过 `CopyDllToProjectRoot` 目标会再复制一份到项目根：`DualRoleAdventure.dll`。
- **部署、联调、对外引用统一使用项目根 `DualRoleAdventure.dll`，不要使用 `.godot/mono/temp/bin/...` 下的临时 DLL。**
- 代理在日志说明中若提到 DLL 路径，优先写项目根路径，避免误导。

### 5.2 代码格式与静态检查

- 检查格式（不改文件）：`dotnet format LocalMultiControl.csproj --verify-no-changes`
- 自动格式化：`dotnet format LocalMultiControl.csproj`

说明：当前未配置独立 lint 工具，以 `dotnet format` + 编译警告作为基础质量门禁。

### 5.3 测试

- 当前仓库暂无测试项目。
- 若后续新增测试项目，默认命令：
  - 全量：`dotnet test <TestProject.csproj> -c Debug`
  - 单测：`dotnet test <TestProject.csproj> --filter "FullyQualifiedName~Namespace.ClassName.MethodName"`

## 6. 运行与日志验证

- 构建后 DLL 通过 `LocalMultiControl.csproj` 的 `Copy Mod` 目标复制到游戏 `mods` 目录。
- 日志路径：`C:\Users\temp\AppData\Roaming\SlayTheSpire2\logs\godot.log`
- 日志级别：优先 `Log.Info`（`Log.Debug` 默认不可见）。
- 日志前缀统一：`[LocalMultiControl]`

建议验证用例（后续功能开发时使用）：

- 多输入源同时控制时，不出现状态覆盖或卡死。
- 场景切换后输入映射保持正确，无残留绑定。
- 本地多控与原始单控互转时，控制权回收正确。

## 7. C# 代码风格

### 6.1 导入与命名空间

- `using` 分组：系统库 → 第三方库 → 项目内命名空间。
- 删除未使用 `using`。
- 文件作用域命名空间示例：`namespace LocalMultiControl.Scripts.Patch;`

### 6.2 命名规则

- 类型/方法/属性：`PascalCase`
- 局部变量/参数/私有字段：`camelCase`
- 私有字段前缀 `_`
- 常量：`SCREAMING_SNAKE_CASE`（若项目中已使用）

### 6.3 类型与可空

- 保持显式类型，避免无意义 `var`。
- 已启用 `<Nullable>enable</Nullable>`，必须处理 `null` 分支。
- 对外可空引用使用 `?`，访问前做守卫。

## 8. Harmony 补丁规范

- 优先 `Postfix` 做附加行为，`Prefix` 做前置保护。
- 高频路径补丁必须轻量，避免反射重逻辑。
- 补丁命名建议：`PrefixXxx` / `PostfixXxx`。

## 9. 领域知识要点（联机转本地多控）

- 核心问题是“输入源聚合”和“控制权分发”，而非单纯 UI 变更。
- 要优先保证状态一致性：战斗、地图、奖励等阶段切换时控制上下文不能错位。
- 任何本地镜像状态都不能污染底层权威数据结构。
- 对“接管/释放控制权”必须幂等，重复触发不应导致状态乱序。

## 10. 提交改动前自检清单

- 能编译通过：`dotnet build LocalMultiControl.csproj -c Debug`
- 格式通过：`dotnet format LocalMultiControl.csproj --verify-no-changes`
- 关键日志包含 `[LocalMultiControl]`，且无明显异常堆栈

## 11. 推荐工作流（给代理）

- 第一步先阅读：`Scripts/Entry.cs` 与 `Scripts/Patch/` 下相关补丁。
- 第二步小步修改：先建立最小可运行闭环，再扩展功能。
- 第三步本地验证：先构建，再格式检查。
- 第四步日志核对：从 `godot.log` 检查关键路径。
- 第五步提交代码：本会话有改动时执行一次 commit。

## 12. 常见误区与规避

- 不要把临时映射状态直接写回长期运行状态。
- 不要把多个输入源硬编码到单场景生命周期里。
- 不要在高频回调中做重反射与大对象分配。

## 13. 产物复制（部署）

- 本项目当前为纯代码 Mod，不再依赖 pck 产物。
- 本项目内置复制脚本：`copy_pck_to_game.ps1`（脚本名保留兼容，实际仅复制 dll + json）。
- 默认来源：`src/Mods/LocalMultiControl/DualRoleAdventure.dll` 与 `src/Mods/LocalMultiControl/DualRoleAdventure.json`
- 默认目标：`E:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\DualRoleAdventure\`
- 执行示例：`powershell -ExecutionPolicy Bypass -File .\copy_pck_to_game.ps1`

## 14. 规则文件兼容

- 若未来新增 `.cursor/rules/`、`.cursorrules`、`.github/copilot-instructions.md`，需在本文件同步关键约束。
- 本文件是 `src/Mods/LocalMultiControl` 目录下代理执行任务的第一参考文档。

## 15. 会话结束前默认部署动作（新增）

- 当本次会话包含代码修改，且 `dotnet build LocalMultiControl.csproj -c Debug` 成功并产出项目根 `DualRoleAdventure.dll` 后，代理在会话结束前默认执行：`powershell -ExecutionPolicy Bypass -File .\copy_pck_to_game.ps1`。
- 该步骤用于将最新 `dll + json` 应用到游戏 `mods` 目录，作为联调默认收尾动作。
- 若用户在当次会话中明确说明“不执行部署脚本”，则跳过此步骤。

## 16. 构建与复制执行顺序（新增）

- `copy_pck_to_game.ps1` 保持为“仅复制文件”脚本（仅复制 `dll + json`）。
- 当会话内完成代码修改且 DLL 构建成功后，直接执行复制脚本：
  - `powershell -ExecutionPolicy Bypass -File .\copy_pck_to_game.ps1`
- 不再执行 Godot `--export-pack` 打包步骤。

## 17. 复制后自动启动游戏（新增）

- 当执行 `copy_pck_to_game.ps1` 且复制成功后，代理默认自动打开：
  - `steam://rungameid/2868840`
- Windows 环境推荐命令：
  - `Start-Process "steam://rungameid/2868840"`
- 若用户在当次会话中明确说明“不自动启动游戏”，则跳过此步骤。

## 18. 非代码改动跳过部署（新增）

- 当本次会话**未修改代码文件**时，跳过部署与启动流程。
- 代码文件指 `Scripts/` 下的 `*.cs`（含其子目录）；若仅修改 `README.md`、`AGENTS.md`、`task.md`、`.editorconfig`、`.gitattributes` 等文档/配置文件，不执行以下步骤：
  - `copy_pck_to_game.ps1` 复制
  - `Start-Process "steam://rungameid/2868840"` 自动启动游戏
- 若会话中同时存在代码改动与文档改动，仍按既有规则执行部署与启动（除非用户明确要求跳过）。

## 17. 发版流程

- 默认支持“快速发版”：当用户明确要求直接发 Release 时，允许基于当前项目根已有产物直接发布，不强制重新编译。
- 快速发版使用文件：`DualRoleAdventure.dll` 与 `DualRoleAdventure.json`（均位于项目根）。
- **发版改为代理手动执行 `git + gh` 流程（优先）**，不再强依赖 `Scripts/Tools/BuildRelease.ps1`。
  - 推荐顺序：更新版本文件 → `git commit` → `git tag` → `git push` → `gh release create/upload/edit`。
  - 如仅需本地打包，可继续使用 `Scripts/Tools/BuildRelease.ps1` 作为可选工具，但 GitHub 发布默认由代理手动执行。
  - GitHub 发布说明必须为单行文本，**禁止包含 `\n` 字符**（含字面量 `\n` 与真实换行）。
- **发版必须包含对应 zip 包**：脚本会在 `release/` 下生成 `DualRoleAdventure-<version>/` 目录（含 `dll + json`）并同时产出 `DualRoleAdventure-<version>.zip`，不得仅发布散文件。
- **说明文本禁止使用 `\n`**：`DualRoleAdventure.json` 的 `description`、`detail` 不允许包含换行，必须为单行文本；脚本会在发现换行时直接报错终止。
- 发布时必须同时发布与 DLL 同名的 JSON 配置文件（例如：`DualRoleAdventure.json`），格式参考其他 mod 配置：
  ```json
  {
    "id": "DualRoleAdventure",
    "name": "DualRoleAdventure",
    "author": "LocalMultiControl",
    "description": "实现联机转本地多控能力，使单机支持多角色/多输入控制",
    "version": "v1.01",
    "has_pck": false,
    "has_dll": true,
    "dependencies": [],
    "affects_gameplay": true
  }
  ```
- 发布与部署时需保证 `dll + json` 同步到游戏 `mods` 目录，避免仅更新二进制导致版本识别异常。
- 仅在用户明确要求“重编译”或当前产物缺失时，才执行 `dotnet build`。
- 发布时照常创建语义化 tag 与 GitHub Release，填写主要变更说明。
- **每次发版后必须额外提供一条 N 网可用英文更新说明**：
  - 长度不超过 255 个字符；
  - 纯单行英文文本；
  - 不使用 `\n`、不换行、不过度技术化；
  - 回传给用户时必须可直接复制粘贴到发布平台。

## 19. 玩家更新文档维护（新增）

- 功能效果留档统一维护在：`Mod玩家更新与使用说明.md`。
- 当会话内存在有效工作成果（代码、流程、交互或关键修复）并完成提交时，必须同步更新该文档。
- 更新时间轴必须使用逆序（最新日期在最前）。
- 更新要求：按“日期 + 时间段”归并记录重要工作；同一时间段内同类反复调优仅记录一次，避免重复堆叠。
- 文档受众为 Mod 玩家，描述要易懂、偏游戏术语，不使用晦涩技术术语。

## 20. 飞书多维表格任务回写规范（新增）

- 当前任务管理表（开发台账）：
  - `https://my.feishu.cn/wiki/B17OwI4Ymi8E7skp339cFiRHnnf?table=tblsr2MTjWeiuNZ8&view=vewXxBNTOK`
- 当前玩家反馈表（结果视图）：
  - `https://my.feishu.cn/wiki/B17OwI4Ymi8E7skp339cFiRHnnf?table=tblosEdrnEI0L77n&view=vewjPpNXxV`

- 发布release后，代理必须执行“任务回写”：
  - 在任务管理表新增或更新对应记录，不得遗漏。
  - 至少更新字段：`任务描述`、`进展`、`开发状态`、`任务分类`、`最新进展记录`、`提交参考`、`数据来源`。
  - `数据来源` 默认填写 `AI整理`；若来自玩家反馈则填写 `玩家问卷`。
  - `提交参考` 需包含本次提交 hash 与简要 subject，保证可追溯。
  - **禁止仅写一条“发版”任务** 作为回写结果。
  - 发布后必须先基于最近提交日志（`git log`）与 `Mod玩家更新与使用说明.md` 拆分出“具体可读任务”再回写；要求“一个具体任务一条记录”。
  - 拆分粒度至少覆盖：核心功能、关键修复、UI/交互改动、流程/工具改动；同类高频微调可按时间段合并为一条，但不得把多个无关事项合并成“发版”。
  - `任务描述` 必须可让人直接看懂做了什么（例如“修复 Custom 模式点开始报错”），不得使用“发版/收尾/同步”等空泛描述作为唯一任务名。
  - 若补发历史 release 回写，需按玩家文档时间轴补齐近几天漏项，直到主要工作可追溯。

- 推荐状态流转：
  - 新建任务：`开发状态=待确认` 或 `开发中`
  - 完成并提交：`进展=已完成`，`开发状态=已完成`
  - 明确不做：`开发状态=已拒绝`，并在 `最新进展记录` 写明原因

### 20.1 Codex Skill 入口（新增）

- 标准技能文档：`skills/feishu-bitable-sync/SKILL.md`
- 标准执行脚本：`Scripts/Tools/SyncFeishuTask.ps1`
- 代理在执行任务回写时，优先按该 Skill 与脚本执行，避免各会话重复实现。
