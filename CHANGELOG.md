# Changelog

本文件记录 `LocalMultiControl` 的重要版本与关键变更。

## [Unreleased]

## [v1.17] - 2026-03-28

### Changed
- 重构战利品环节：改为按角色独立生成奖励后汇总展示，每个奖励前标识对应角色
- 遗物翻倍、上等好货因子、猎人狩猎等效果现在能正确按角色独立生效
- 移除旧的复制资源镜像逻辑，战利品场景下的遗物/药水/金币镜像被汇总流程抑制

## [v1.10] - 2026-03-22

### Fixed
- 修复瓦库自动切人漏触发：当所有瓦库角色都无牌可出时，新增逐帧兜底自动切到下一位可操作的非瓦库角色。
- 修正瓦库英文文案拼写，统一为 `Vakuu`。

### Changed
- 瓦库开关提示文案统一中英本地化，并在鼠标与手柄切换路径共用同一提示：
- 单角色提示为“瓦库将接管角色X操作 / Vakuu will control Player X”。
- 全体提示为“瓦库将接管所有角色操作 / Vakuu will control all characters”。

## [v1.09] - 2026-03-21

### Added
- 新增手柄组合键：选角界面 `Y` 切换当前角色瓦库，`LT + Y` 切换全体瓦库开关。
- 选角界面新增手柄热键图标提示（`LT + 上下左右`、`Y`、`LT + Y`），并与原版一致仅在手柄模式显示。
- 完善 LT 组合输入链路：LT 持有期间全拦截原生手柄输入，释放时按是否使用组合决定是否回放原 LT 行为。

## [v1.06] - 2026-03-20

### Changed
- 药水改为按角色独立维护：不再固定归属1号位玩家。
- 移除本地多控下“初始额外+2药水位”的特殊规则，恢复联机一致的药水位行为。
- 切人时顶部药水栏会跟随当前控制角色自动刷新，并保持可直接使用。

## [v1.05] - 2026-03-20

### Fixed
- 移除宝箱补丁中的重复复制链路，避免同一次结算出现二次复制。
- 宝箱遗物复制新增“已拥有去重”保护，减少重复遗物写入风险。
- 藏宝图结算改为按全队统一处理，并补齐瓦库流程中的自动视角切换。

## [v0.1.9] - 2026-03-15

### Fixed
- 修复休息区偶发“未手动选牌即随机升级并结束”的问题，恢复战斗外手动选牌。
- 休息区改为固定串行：每位角色各选择一次后才结束，不再遗漏后续角色。
- 事件中的升级/删牌等分支强制对所有未完成角色按同一选项逐次处理。
- 宝箱切人后自动恢复鼠标可见，减少切人后鼠标丢失问题。

## [v0.1.3] - 2026-03-15

### Changed
- 战斗界面切人按钮改为纯图标风格，支持放大与右侧镜像。
- 选人界面右下 2x2 按钮组重排，统一与战斗界面交互样式。
- 多轮截图驱动微调：优化箭头位置、边距、横向间距与可读性。

### Notes
- 对应发布标签：`v0.1.3`
- 对应发布页：<https://github.com/liwenhao0427/STS2_DualRoleAdventure/releases/tag/v0.1.3>

## [v0.1.2] - 2026-03-14

### Changed
- 调整发版流程，支持“快速发版”（可直接基于项目根已有产物发布）。

## [v0.1.1-clearable] - 2026-03-14

### Fixed
- 修复入战斗顶部栏显示异常。
- 修复共享事件自动代投相关卡流程问题。

## [v0.1.0-initial-usable] - 2026-03-13

### Added
- 建立本地多控最小可用闭环。
- 基础输入切换、关键同步链路与核心补丁框架落地。

[Unreleased]: https://github.com/liwenhao0427/STS2_DualRoleAdventure/compare/v1.10...HEAD
[v1.10]: https://github.com/liwenhao0427/STS2_DualRoleAdventure/releases/tag/v1.10
[v1.09]: https://github.com/liwenhao0427/STS2_DualRoleAdventure/releases/tag/v1.09
[v1.06]: https://github.com/liwenhao0427/STS2_DualRoleAdventure/releases/tag/v1.06
[v1.05]: https://github.com/liwenhao0427/STS2_DualRoleAdventure/releases/tag/v1.05
[v0.1.9]: https://github.com/liwenhao0427/STS2_DualRoleAdventure/releases/tag/v0.1.9
[v0.1.3]: https://github.com/liwenhao0427/STS2_DualRoleAdventure/releases/tag/v0.1.3
[v0.1.2]: https://github.com/liwenhao0427/STS2_DualRoleAdventure/releases/tag/v0.1.2
[v0.1.1-clearable]: https://github.com/liwenhao0427/STS2_DualRoleAdventure/releases/tag/v0.1.1-clearable
[v0.1.0-initial-usable]: https://github.com/liwenhao0427/STS2_DualRoleAdventure/releases/tag/v0.1.0-initial-usable
