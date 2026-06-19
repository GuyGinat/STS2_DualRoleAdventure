# 待修复问题记录

## 问题1：卡牌奖励 - 搜刮/战斗后卡牌奖励

**用户需求**：
- 战斗后卡牌奖励页面显示"将一张牌添加到你的牌组"
- 希望多给一组奖励（另一角色的卡牌池的三选一）

**当前状态**：
- 日志显示 "卡牌奖励已添加额外组"
- 但实际没有显示多一组

**根本原因分析**：
- 当前使用 `combatRoom.AddExtraReward(otherPlayer, extraReward)` 添加额外奖励
- 但在 RewardsSet.cs 第58-61行，额外奖励是按 Player 来获取的：
  ```csharp
  if (Room is CombatRoom combatRoom && combatRoom.ExtraRewards.TryGetValue(Player, out List<Reward> value))
  {
      Rewards.AddRange(value);
  }
  ```
- 遍历奖励时用的是 currentPlayer，但额外奖励是添加给 otherPlayer 的
- 所以 currentPlayer 的 ExtraRewards 里没有额外奖励

**修复方向**：
- 不应该使用 AddExtraReward
- 应该直接在 CardReward 的 _cards 列表中添加来自其他角色卡池的卡牌
- 或者修改 RewardsSet 来支持额外奖励

---

## 问题2：宝箱问题

**用户需求**：
- 进入宝箱事件，一个人选完后就卡死
- 另一角色应该直接获取奖励，不需要选择

**当前状态**：
- 日志显示 OnPicked 补丁触发，"本地双人模式已自动补齐宝箱投票"
- 但补齐后报错："Attempted to pick relic while relic picking is not active!"

**根本原因分析**：
- OnPicked 被触发后，我们调用了 `AwardRelics()` 和 `EndRelicVoting()`
- 这导致 `_currentRelics = null`，relic picking 结束
- 但 UI 仍然尝试让玩家继续选择遗物（点击另一个遗物索引）
- 因为 2号玩家也需要选择，但 picking 已经结束

**修复方向**：
- 问题在于投票补齐后立即结束了 relic picking
- 需要在 OnPicked 时，为另一玩家自动投票（已完成）
- 但不应该立即调用 AwardRelics 和 EndRelicVoting
- 应该让原逻辑继续执行，等待所有玩家都投票后再AwardRelics

---

## 问题3：药水栏 - 购买和显示问题

**用户需求**：
- 药水始终归属1号位
- 药水栏位增加2格
- 药水栏始终可用且正确显示1号位的药水

**当前状态**：
- 2号位购买药水不扣钱
- 药水没买成功
- 1号位背包多了药水，占用栏位
- UI不显示，无法使用

**根本原因分析**：
- 禁用了 NPotionContainerPatch，所以药水栏显示有问题
- 需要恢复并修改为始终显示1号位（PrimaryPlayer）的药水

**修复方向**：
- 恢复 NPotionContainerPatch
- 修改逻辑：始终显示 PrimaryPlayer 的药水栏
- 需要找到药水栏初始化的逻辑，绑定到 PrimaryPlayer

---

## 问题4：战斗中药水使用问题

**用户需求**：
- 多人模式下应该可以选择药水目标（给谁用）
- 增益药水默认以当前角色为准，而不是固定1号位

**当前状态**：
- 当前药水固定归属1号位
- 战斗中不能选择目标

**修复方向**：
- 药水使用逻辑需要支持目标选择
- 增益药水效果应以当前控制角色为准

---

## 相关文件

- `Scripts/Patch/CardRewardPatch.cs` - 卡牌奖励补丁
- `Scripts/Patch/TreasureRoomRelicSynchronizerPatch.cs` - 宝箱补丁
- `Scripts/Patch/NPotionContainerPatch.cs` - 药水栏补丁（当前被禁用）
- `Scripts/Patch/PlayerPotionMirrorPatch.cs` - 药水归属补丁
- `src/Core/Rewards/RewardsSet.cs` - 奖励生成逻辑（第58-61行）
- `src/Core/Rooms/CombatRoom.cs` - 额外奖励存储

## 日志参考

```
// 卡牌奖励 - 添加了但没显示
[INFO] [LocalMultiControl] 卡牌奖励已添加额外组: currentPlayer=76561198388115947, otherPlayer=76561198388115946

// 宝箱 - 投票补齐后卡死
[DEBUG] [TreasureRoomRelicSynchronizer] Player ... picked relic at index 0: RELIC.PRAYER_WHEEL
[INFO] [LocalMultiControl] 本地双人模式已自动补齐宝箱投票（随机），按简化随机宝箱流程结算。
[DEBUG] [TreasureRoomRelicSynchronizer] Relic index 1 () is being picked by local player ...
ERROR: System.InvalidOperationException: Attempted to pick relic while relic picking is not active!

// 药水
[INFO] [LocalMultiControl] 药水已固定归属1号位: FIRE_POTION, from=76561198388115947, to=76561198388115946
[WARN] [LocalMultiControl] 跳过药水动画：当前视图不存在药水 FIRE_POTION
```
