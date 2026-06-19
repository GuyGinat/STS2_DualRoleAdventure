# LocalMultiControl（DualRoleAdventure）

`LocalMultiControl` 是《Slay the Spire 2》的本地多控 Mod。
目标是把联机控制链路复用到单机环境，实现 `2~12 人本地多控`（支持重复选角），并尽量保持原联机流程的一致性。

## 现在能用到什么（玩家视角）

- 可以在单机里体验本地多角色玩法，并在关键场景切换操控角色。
- 从大厅到战斗、事件、奖励、下一幕，主流程基本可跑通。
- 常见“卡住不动”“按钮不好点”“切人后界面不对”的问题已做多轮修复。
- 按钮和入口交互已经过持续打磨，新玩家上手成本更低。

## 建议你这样体验

1. 先用 2 人开局，熟悉切人按钮和切换节奏。
2. 重点体验：战斗切人、事件选择、领奖励这三段是否顺手。
3. 若遇到异常，优先记录“在哪一幕、哪个界面、做了什么操作”再反馈，定位会更快。

## 当前能力（开发视角）

- 本地多人组队：支持 2~12 人。
- 角色控制切换：支持热键切换与界面按钮切换。
- 战斗/选人/大厅关键界面补丁：提供可视化切人操作。
- 关键流程同步：覆盖奖励、事件、商店、遗物/药水等核心链路。

## baselib 使用状态

- 当前 **未使用 baselib**。

## 目录说明

```text
src/Mods/LocalMultiControl/
├─ Scripts/
│  ├─ Entry.cs
│  ├─ Patch/
│  └─ Runtime/
├─ LocalMultiControl.csproj
├─ mod_manifest.json
├─ README.md
└─ CHANGELOG.md
```

## 构建与检查

在 `src/Mods/LocalMultiControl` 目录执行：

```powershell
dotnet restore LocalMultiControl.csproj
dotnet build LocalMultiControl.csproj -c Debug
dotnet format LocalMultiControl.csproj --verify-no-changes
```

## 部署与联调

1. 导出 pck：

```powershell
& "C:\Users\temp\项目\杀戮尖塔2Mod\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64.exe" --path . --export-pack "Windows Desktop" DualRoleAdventure.pck
```

2. 复制到游戏目录：

```powershell
powershell -ExecutionPolicy Bypass -File .\copy_pck_to_game.ps1
```

3. 启动游戏：

```powershell
Start-Process "steam://rungameid/2868840"
```

说明：部署使用项目根产物 `DualRoleAdventure.dll` 与 `DualRoleAdventure.pck`。

## 发布说明

- 默认支持快速发版：若项目根已有最新 `DualRoleAdventure.dll` 与 `DualRoleAdventure.pck`，可直接创建 tag + GitHub Release。
- 仅在用户明确要求重编译/重导出或产物缺失时，执行完整构建与导出流程。

## 玩家反馈与任务台账（飞书多维表格）

- 任务管理表（AI 与开发台账）：
  - https://my.feishu.cn/wiki/B17OwI4Ymi8E7skp339cFiRHnnf?table=tblsr2MTjWeiuNZ8&view=vewXxBNTOK
- 玩家反馈处理结果（开发侧结果看板）：
  - https://my.feishu.cn/wiki/B17OwI4Ymi8E7skp339cFiRHnnf?table=tblosEdrnEI0L77n&view=vewjPpNXxV
- 玩家问卷表单（当前使用，玩家可直接提交 Bug/建议）：
  - https://my.feishu.cn/share/base/form/shrcn5DNr4nXrBDHMkLrHaEaVyh

### 字段约定

- 主任务表（`✅任务管理`）新增字段：
  - `数据来源`：`AI整理` / `玩家问卷`
  - `开发状态`：`待确认` / `开发中` / `已完成` / `已拒绝`
  - `任务分类`：`Bug修复` / `需求` / `发布` / `优化`
  - `提交参考`：对应 git commit 摘要（hash + subject）
- 玩家问卷表（`📝玩家反馈问卷`）核心字段：
  - `反馈标题`
  - `反馈详情`
  - `反馈分类`（Bug/建议）
  - `来源`（默认玩家问卷）
  - `开发状态`
  - `提交时间`
  - `联系方式`（选填）
  - `游戏版本`
  - `复现步骤`

### 使用建议

1. 玩家通过“玩家问卷表单”提交后，先将 `开发状态` 标成 `待确认`。
2. 评审后将确认项同步到主任务表，`数据来源` 填 `玩家问卷`。
3. 开发完成时同步更新两表状态，保证“玩家视角”和“开发视角”可追踪。

### Codex Skill（开发者）

- 技能文档：`skills/feishu-bitable-sync/SKILL.md`
- 同步脚本：`Scripts/Tools/SyncFeishuTask.ps1`
- 常用命令：

```powershell
powershell -ExecutionPolicy Bypass -File .\Scripts\Tools\SyncFeishuTask.ps1 `
  -TaskDescription "本次任务标题" `
  -LatestNote "本次完成内容摘要" `
  -Progress "已完成" `
  -DevStatus "已完成" `
  -Category "Bug修复" `
  -Source "AI整理"
```

## 支持作者

如果这个 Mod 对你有帮助，欢迎请作者喝杯咖啡 ☕

<img src="捐赠支持作者开发.jpeg" alt="捐赠支持作者开发" width="200" />

## 修改日志（重要里程碑）

详细记录见 [CHANGELOG.md](./CHANGELOG.md)。

- `v0.1.9`（2026-03-15）：修复休息区与事件“强制每人各处理一次”，并恢复宝箱切人后鼠标可见。
- `v0.1.3`（2026-03-15）：切人与加减按钮样式重做（图标化、镜像、间距与位置微调），提升战斗/选人 UI 一致性。
- `v0.1.2`（2026-03-14）：发版规范升级为快速发布流程。
- `v0.1.1-clearable`（2026-03-14）：修复入战斗 UI 显示与共享事件自动代投关键问题。
- `v0.1.0-initial-usable`（2026-03-13）：形成可用最小闭环，打通本地多控基础流程。
