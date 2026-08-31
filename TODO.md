# Known Issues & Requests

Status as of 2026-08-31 (post-v1.32, unreleased work in the repo). The four issues
carried over from the original author's notes were re-audited against the current
code; two were already resolved by earlier redesigns.

## Open — needs reporter logs / repro

### A. Black screen + hard lock in the room after a rest site (Workshop, 2026-08-26, v0.111)
Reporter: 萧统. No log yet. The mod's rest-site option flow (RestSitePatch) traces clean;
suspects are the next-room transition (map vote → room build) or an event room rebuild.
Ask for `%APPDATA%/SlayTheSpire2/logs/godot.log` and whether it happens at every campfire.

### B. Selection-type potions used cross-character do nothing (Workshop, 2026-08-29, v0.111)
Reporter: LH. Slot 2 uses a choose-a-card potion (e.g. Droplet of Precognition / Liquid
Memories, `TargetType.AnyPlayer`) on slot 1 → no effect; on self it works.
Static trace of the whole pipeline finds every known hole already patched:
- `CardSelectCmd.ShouldSelectLocalCard` → forced local for all local characters (CardSelectCmdPatch)
- `PlayerChoiceSynchronizer.SyncLocalChoice` → sender/context switched to the choosing player (PlayerChoiceContextPatch)
- `GameActionPlayerChoiceContext/HookPlayerChoiceContext.SignalPlayerChoiceEnded` → resume forced unconditionally
- Host-side `RequestResumeActionAfterPlayerChoice` → resumes with no owner validation
So the failure is somewhere runtime-only; UsePotionActionWatchdogPatch should log
"药水动作等待选择超过2000ms" if the action stalls. Need the reporter's log. Also verify
where the selected card lands (it goes to the TARGET's hand, which is backgrounded —
could read as "no effect" if the user doesn't switch).

### C. Heir skipped a draw step in the final-act Queen fight (Workshop, 2026-08-29, v0.111)
Reporter: LH. After Torch Head died, one turn the Heir (many powers active) drew nothing;
hand held a single Spectral colorless. Unclear if mod-related (turn mirroring) or a game
quirk. Needs repro/log; check CombatManagerReadyEnemyTurnPatch interplay with extra-turn
players (`CombatTurnState.PlayersTakingExtraTurn`) if it recurs.

## Feature requests

### D. Keyboard controls one character, gamepad the other (Workshop, 2026-08-25)
Large: needs per-device input routing to different characters. The mod already has a
gamepad axis router; not started.

## Resolved by re-audit (needs playtest confirmation only)

### ~~Issue 1: Extra card group after combat~~ (IMPLEMENTED, opt-in, unreleased)
Now generated inside CombatRoomOfferRewardsPatch for each character from the other
characters' pools. Off by default: set `"extraCrossCharacterCardReward": true` in
`user://dual_role_adventure_settings.json`. The original `AddExtraReward(otherPlayer, ...)`
approach failed because RewardsSet fetches ExtraRewards keyed by its own player.

### ~~Issue 2: Treasure chest deadlock~~ (RESOLVED by earlier redesign)
Current flow: per-character voting with auto-switch (TreasureRoomRelicSynchronizerPatch)
plus `_localPlayerId` re-sync on switch (LocalMultiControlRuntime:486). Vanilla's
IsSingleplayerOrFakeMultiplayer auto-random-vote path is inactive (loopback Type=Host), and
the old "pick while not active" crash is a benign warning in v0.111.

### ~~Issue 3: Potion bar purchase/display~~ (RESOLVED by earlier work)
NPotionContainerPatch is active again: the bar rebinds (holders rebuilt, events reconnected)
to the controlled character on Initialize/switch. Buying with slot 2 needs a playtest pass
to confirm gold deduction; if still broken, capture a log.

### ~~Issue 4: Potion targeting in combat~~ (PARTLY RESOLVED)
Buff (self-target) potions follow the controlled character (PotionManualUseTargetPatch).
Remaining piece is open item B above (cross-character selection potions).

### ~~Issue 5: Silken Tress — Glam enchant not applied~~ (FIXED in repo, unreleased)
Root cause: double reward generation. Fixed by CombatRoomOfferRewardsPatch (see CHANGELOG).
Verify: Neow → Silken Tress → first combat reward offers Glam cards.
