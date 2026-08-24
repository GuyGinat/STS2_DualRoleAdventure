# Known Issues Under Investigation

Carried over from the original author's notes (translated); log excerpts are quoted verbatim from runtime output.

## Issue 1: Card rewards — extra card group after combat/scavenge

**Desired behavior:**
- The post-combat card reward page shows "Add a card to your deck"
- It should offer an *additional* group (a pick-1-of-3 from the other character's card pool)

**Current state:**
- The log reports "卡牌奖励已添加额外组" (extra card group added)
- But no extra group actually appears

**Root cause analysis:**
- The code uses `combatRoom.AddExtraReward(otherPlayer, extraReward)`
- But in `RewardsSet.cs` (~line 58), extra rewards are fetched **by player**:
  ```csharp
  if (Room is CombatRoom combatRoom && combatRoom.ExtraRewards.TryGetValue(Player, out List<Reward> value))
  {
      Rewards.AddRange(value);
  }
  ```
- Reward iteration runs as `currentPlayer`, but the extra reward was registered for `otherPlayer`
- So `currentPlayer`'s `ExtraRewards` never contains the extra group

**Fix direction:**
- Don't use `AddExtraReward`
- Either add cards from the other character's pool directly into the `CardReward`'s `_cards` list, or patch `RewardsSet` to support cross-player extra rewards

---

## Issue 2: Treasure chest deadlock

**Desired behavior:**
- In a chest room, after one character picks, the other should receive their reward directly without choosing

**Current state:**
- The `OnPicked` patch fires and logs "本地双人模式已自动补齐宝箱投票" (auto-completed chest vote)
- Then it errors: `Attempted to pick relic while relic picking is not active!`

**Root cause analysis:**
- After `OnPicked`, the mod calls `AwardRelics()` and `EndRelicVoting()`
- That sets `_currentRelics = null`, ending relic picking
- But the UI still lets the second player click another relic index — picking has already ended

**Fix direction:**
- Auto-vote for the other player on `OnPicked` (already done), but do **not** immediately call `AwardRelics`/`EndRelicVoting`
- Let the original logic run and award once all players have voted

---

## Issue 3: Potion bar — purchase and display problems

**Desired behavior:**
- Potions always belong to slot 1 (primary player)
- Potion bar gains 2 extra slots
- The bar is always usable and correctly shows slot 1's potions

**Current state:**
- Slot 2 buying a potion doesn't deduct gold, and the potion isn't actually acquired
- Slot 1's inventory gains a phantom potion occupying a slot
- The UI doesn't display it and it can't be used

**Root cause analysis:**
- `NPotionContainerPatch` is currently disabled, so the potion bar display is broken
- It needs to be restored and changed to always display the primary player's potions

**Fix direction:**
- Re-enable `NPotionContainerPatch`
- Bind the potion bar to the primary player; find the bar's initialization logic and pin it there

---

## Issue 4: Using potions in combat

**Desired behavior:**
- Multi-character mode should allow choosing a potion *target* (which character receives it)
- Buff potions should default to the currently controlled character, not always slot 1

**Current state:**
- Potions are pinned to slot 1
- No target selection in combat

**Fix direction:**
- Potion-use logic needs target selection support
- Buff potion effects should apply to the currently controlled character

---

## Related files

- `Scripts/Patch/CardRewardPatch.cs` — card reward patch
- `Scripts/Patch/TreasureRoomRelicSynchronizerPatch.cs` — treasure chest patch
- `Scripts/Patch/NPotionContainerPatch.cs` — potion bar patch (currently disabled)
- `Scripts/Patch/PlayerPotionMirrorPatch.cs` — potion ownership patch
- `src/Core/Rewards/RewardsSet.cs` — reward generation (decompiled reference)
- `src/Core/Rooms/CombatRoom.cs` — extra reward storage (decompiled reference)

## Log reference

```
// Card reward — added but not shown
[INFO] [LocalMultiControl] 卡牌奖励已添加额外组: currentPlayer=76561198388115947, otherPlayer=76561198388115946

// Treasure chest — deadlock after vote auto-completion
[DEBUG] [TreasureRoomRelicSynchronizer] Player ... picked relic at index 0: RELIC.PRAYER_WHEEL
[INFO] [LocalMultiControl] 本地双人模式已自动补齐宝箱投票（随机），按简化随机宝箱流程结算。
[DEBUG] [TreasureRoomRelicSynchronizer] Relic index 1 () is being picked by local player ...
ERROR: System.InvalidOperationException: Attempted to pick relic while relic picking is not active!

// Potions
[INFO] [LocalMultiControl] 药水已固定归属1号位: FIRE_POTION, from=76561198388115947, to=76561198388115946
[WARN] [LocalMultiControl] 跳过药水动画：当前视图不存在药水 FIRE_POTION
```

---

## ~~Issue 5: Silken Tress — Glam enchant not applied~~ (FIXED, unreleased)

**Report (issaclai27, Workshop comments, on v1.31 / game v0.109):** picking up Silken Tress removed all gold as expected, but the first card reward afterwards had no card enchanted with Glam.

**Root cause (confirmed by code trace):** double generation. Vanilla `CombatRoom.OfferRoomEndRewards` generates all players' reward sets (hooks run, Silken Tress enchants + burns its one-shot `IsUsed`) before calling `Offer`; the mod's merge patch on `Offer` discarded those sets and regenerated — the second pass saw `IsUsed == true`. Fixed by moving the merge up to `CombatRoom.OfferRoomEndRewards` (`Scripts/Patch/CombatRoomOfferRewardsPatch.cs`) so generation happens exactly once. Needs an in-game verification run (Neow → take Silken Tress → first combat reward should offer Glam cards).
