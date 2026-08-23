# Changelog

Notable versions and key changes of `LocalMultiControl` / `DualRoleAdventure`. Entries up to v1.30 are translated from the original author's Chinese changelog; the fuller day-by-day history lives in `docs/archive/player-update-history.zh.md`.

## [Unreleased]

## [v1.32] - 2026-08-23

Compatibility release. The mod stopped loading after the game's v0.110.0 / v0.111.0 patches
(reported on the Workshop page by several players).

### Fixed
- Adapted to game v0.111.0 (2026-08-13). Compile-level API breaks since v0.109.0:
  - `LobbyPlayer` was renamed `StartRunLobbyPlayer` (same layout; the 16-player slot-id bit-width transpilers now target it).
  - `INetGameService` gained `LocalVersion`; `INetHostGameService` gained `ClientConnectionFailed` and `GetVersionInfoForPeer`. The loopback service reports `PeerVersionInfo.LocalDefault()` for every peer — the game dereferences `GetVersionInfoForPeer(...).Value` unguarded in all three lobbies, so it must never be null.
  - `ILoadRunLobbyListener.PlayerConnected` now takes a `LoadRunLobbyPlayer` instead of a `ulong`.
  - `LoadRunLobby` dropped `ConnectedPlayerIds` and the private `_readyPlayers` set; membership and readiness both live in the public `Players` list of `LoadRunLobbyPlayer` structs. The load-save auto-ready patch now adds/readies the other local characters in that list (previously it reflected on `_readyPlayers`, which would have silently done nothing).
  - `StartRunLobby.MaxPlayers` became the private readonly field `_maxPlayers`; the 12-character capacity raise now targets that field.
  - `NControllerManager.IsUsingController` was removed; controller-hint checks use `InputType == InputType.Controller`.
- Runtime (string-reflection) break: `CombatManager._playersReadyToBeginEnemyTurn` no longer exists. Combat ready sets moved into an internal `CombatTurnState` (`_turnState`) guarded by `ReadyLock`, and the player→enemy transition is now a `BeginEnemyTurnSignalSource` task awaited by the turn loop. The enemy-turn auto-ready patch now mirrors readiness into `turnState.PlayersReadyToBeginEnemyTurn` under the lock, and its fallback completes the signal (idempotent) instead of invoking `AfterAllPlayersReadyToBeginEnemyTurn` directly, which would now run the transition twice.

### Notes
- Static sweep of all string-based Harmony/reflection targets against decompiled v0.111.0: 163 resolve. Remaining misses are the intentional legacy-name fallbacks (`BeginRunIfAllPlayersReady`) and the already-guarded `NRestSiteRoom.UpdateNavigation`.
- Unverified in-game at time of writing — needs a playtest pass (lobby → combat end-turn with 2+ characters → rewards → save/continue).

## [v1.31] - 2026-07-27

First community-maintained release. The original author (liwenhao0427) discontinued
maintenance at v1.30 and gave written permission (2026-07-27, email) for this fork to
take over maintenance and distribution; they will cross-link this version from the
original Workshop item and video.

### Added
- Optional "ghost hands" combat overlay: shows every backgrounded character's current hand as rows of non-interactive cards behind and above the active character's hand. Toggle with `F8`; move the display at runtime with `Ctrl+Arrows` (`Ctrl+Shift+Arrows` for fine 4px steps). State, position and scale persist to `user://dual_role_adventure_settings.json` (edit `ghostHandsScale` there to resize; default 0.5). Card nodes are borrowed from and returned to the game's own `NodePool`.

### Changed
- Character switching is now bound to `Tab` (next) and `Shift+Tab` (previous). The legacy keys — `]` / `R` / `/` for next, `[` / `T` for previous — still work as aliases.
- Documentation translated to English; original Chinese documents archived under `docs/archive/`.

### Fixed
- Adapted to game v0.109.0 (2026-07-17); five compile-level API breaks since the v1.30 baseline:
  - `VoteToMoveToNextActAction` gained a required `currentActIndex` parameter; the act-change auto-ready patch now passes `RunState.CurrentActIndex`, mirroring the game's own call site.
  - `PotionFactory.CreateRandomPotionsOutOfCombat` now returns `IEnumerable<PotionModel>`; the local merchant inventory rebuild materializes it with `.ToList()` like the game does.
  - `Controller` input constants were renamed (`joystick*` → `lStick*`, `dPad{East,West,North,South}` → `dPad{Right,Left,Up,Down}`); updated the gamepad axis router and the character-select hotkey hint icons.
  - `EventModel.GenerateInternalCombatState(runState)` was removed; rebuilding a non-shared combat-layout event room on character switch now calls `EventSynchronizer.GenerateInternalCombatStateIfNecessary(event)` instead.
- Local Multi-Control entry unreachable on fresh profiles: since v0.109.0, pressing Host with `Progress.NumberOfRuns == 0` skips the host submenu and immediately hosts a Standard online game, so the injected card never appeared. A new prefix on `NMultiplayerSubmenu.OnHostPressed` routes fresh profiles through the host submenu (its Standard card keeps the one-click hosting behavior).

### Notes
- `NRestSiteRoom.UpdateNavigation` no longer exists in v0.109.0; the rest-site controller focus recovery already guarded that lookup with a null-conditional call, so it degrades to the mod's own focus-grab fallback.
- Static sweep of all string-based Harmony/reflection targets against decompiled v0.109.0 source: everything else resolves (the two `BeginRunIfAllPlayersReady` misses are the intentional legacy-name fallback legs).

## [v1.30] - 2026-06-19

Final release by the original author. (Summarized from the archived player-update history.)

### Fixed
- Adapted to the game's 1.0 release: treasure rooms no longer black-screen (the removed `IsSinglePlayerOrFakeMultiplayer` API is no longer called; the chest gesture layer works with the new vote structure, with a focus guard for first-frame relic nodes).
- Shop inventory switching adapted to the new `Inventories` structure; ESC quick-restart moved to the release version's load method.
- Loading a multi-character save resumes correctly (`BeginRunForAllPlayersIfAllReady` with a reflection fallback to the legacy `BeginRunIfAllPlayersReady`).
- Reward synchronizer local-player checks (`RewardsSetSynchronizer`) are re-synced on character switch, fixing "clicked a relic reward, nothing happened" in the beta.

## [v1.17] - 2026-03-28

### Changed
- Reworked the loot phase: rewards are generated independently per character, then presented as one combined list with a per-character prefix label.
- Relic-doubling effects, shop-quality factors, and hunt-style card effects now apply per owning character.
- Removed the old resource-mirroring logic; relic/potion/gold mirroring in loot scenes is suppressed by the aggregated flow.

## [v1.10] - 2026-03-22

### Fixed
- Vakuu auto-switch could miss its trigger: added a per-frame fallback that switches to the next controllable non-Vakuu character when no Vakuu character has a playable card.
- Standardized the English spelling `Vakuu`.

### Changed
- Vakuu toggle hint text unified across mouse and controller paths, localized in Chinese and English (single: "Vakuu will control Player X"; all: "Vakuu will control all characters").

## [v1.09] - 2026-03-21

### Added
- Controller combos on the character-select screen: `Y` toggles Vakuu for the highlighted character, `LT + Y` toggles all.
- Controller hotkey hint icons on the select screen (`LT + D-pad`, `Y`, `LT + Y`), shown only in controller mode like the base game.
- Full LT-combo input chain: while LT is held, native controller input is intercepted; on release, the original LT action replays only if no combo was used.

## [v1.06] - 2026-03-20

### Changed
- Potions are now maintained per character instead of being pinned to slot 1.
- Removed the special "+2 initial potion slots" rule; potion slots match online multiplayer behavior.
- The top potion bar follows the currently controlled character after switching and stays directly usable.

## [v1.05] - 2026-03-20

### Fixed
- Removed a duplicate copy path in the treasure chest patch that could double-copy in a single settlement.
- Chest relic copying now de-duplicates against already-owned relics.
- Treasure-map settlement handled once for the whole party, with automatic view switching during Vakuu flows.

## [v0.1.9] - 2026-03-15

### Fixed
- Rest sites no longer occasionally auto-upgrade a random card and end without manual selection; manual card selection outside combat restored.
- Rest sites run strictly serially: each character chooses once before the site ends — no more skipped characters.
- Event branches that upgrade/remove cards process every unfinished character with the same option, one by one.
- Mouse visibility restored after switching characters at a treasure chest.

## [v0.1.3] - 2026-03-15

### Changed
- Combat character-switch buttons redesigned as pure icons with scaling and right-side mirroring.
- Character-select 2x2 button group rearranged to match combat-screen interaction style.
- Multiple rounds of screenshot-driven tuning: arrow positions, margins, horizontal spacing, readability.

## [v0.1.2] - 2026-03-14

### Changed
- Release process upgraded to support "fast releases" (publish directly from existing artifacts in the project root).

## [v0.1.1-clearable] - 2026-03-14

### Fixed
- Combat top-bar display glitch on entering combat.
- Shared-event auto-voting flow blockage.

## [v0.1.0-initial-usable] - 2026-03-13

### Added
- Minimal usable loop for local multi-control.
- Basic input switching, key synchronization chains, and the core patch framework.

[Unreleased]: https://github.com/GuyGinat/STS2_DualRoleAdventure/compare/v1.31...HEAD
[v1.31]: https://github.com/GuyGinat/STS2_DualRoleAdventure/releases/tag/v1.31
[v1.30]: https://github.com/liwenhao0427/STS2_DualRoleAdventure/releases/tag/v1.30
[v1.17]: https://github.com/liwenhao0427/STS2_DualRoleAdventure/releases/tag/v1.17
[v1.10]: https://github.com/liwenhao0427/STS2_DualRoleAdventure/releases/tag/v1.10
[v1.09]: https://github.com/liwenhao0427/STS2_DualRoleAdventure/releases/tag/v1.09
[v1.06]: https://github.com/liwenhao0427/STS2_DualRoleAdventure/releases/tag/v1.06
[v1.05]: https://github.com/liwenhao0427/STS2_DualRoleAdventure/releases/tag/v1.05
[v0.1.9]: https://github.com/liwenhao0427/STS2_DualRoleAdventure/releases/tag/v0.1.9
[v0.1.3]: https://github.com/liwenhao0427/STS2_DualRoleAdventure/releases/tag/v0.1.3
[v0.1.2]: https://github.com/liwenhao0427/STS2_DualRoleAdventure/releases/tag/v0.1.2
[v0.1.1-clearable]: https://github.com/liwenhao0427/STS2_DualRoleAdventure/releases/tag/v0.1.1-clearable
[v0.1.0-initial-usable]: https://github.com/liwenhao0427/STS2_DualRoleAdventure/releases/tag/v0.1.0-initial-usable
