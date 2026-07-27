# Design Doc: Online Multiplayer → Local Multi-Control (translated)

> Historical document — the original author's confirmed requirements spec (March 2026), translated from Chinese. Kept for background; current behavior is described in `PLAYER_GUIDE.md` and `docs/architecture.md`.

## 1. Goal & scope

- Add a "play with yourself" entry to the multiplayer system: one process controls multiple characters, reusing the existing multiplayer flow wherever possible.
- Standard runs first; Daily/Custom later on the same architecture.
- Experience goals: freely add local "pseudo-players" in the lobby; switch controlled character via hotkeys in combat; rewards/shop/events/rest/map votes behave like online multiplayer; resources (deck, gold, potions, relics, event history) stay owned per character.

## 2. Source-code findings (key facts)

- Multiplayer host entry: `NMultiplayerHostSubmenu`; standard character select: `NCharacterSelectScreen`; lobby list & readiness: `StartRunLobby`.
- Runtime sync is initialized by `RunManager`: `PlayerChoiceSynchronizer`, `MapSelectionSynchronizer`, `EventSynchronizer`, `RewardSynchronizer`, `RestSiteSynchronizer`, `OneOffSynchronizer`, `TreasureRoomRelicSynchronizer` — all keyed by player NetId, which matches the per-character ownership goal.
- Single-player limitation: `NetSingleplayerGameService` pins `NetId = 1`; `LocalContext.NetId` is set from `NetService.NetId` at `RunManager.Launch()`, and most UI logic asks `LocalContext.GetMe()`. Conclusion: without a mutable-NetId strategy, the stock single-player service cannot host multiple switchable local characters.

## 3. Architecture (as adopted)

1. **Local multi-control session layer** — local pseudo-player list (playerId/slot/character/ready), current controlled player, `SwitchNextPlayer()`/`SwitchPrevPlayer()`.
2. **Network adaptation layer** — a loopback `INetGameService` implementation (became `LocalLoopbackHostGameService`): synchronizers think it's a multiplayer session; messages are dispatched in-process; the sender ID is configurable per current controlled player.
3. **Control-context switching layer** — switching updates `LocalContext.NetId` and input-routing ownership (hand, end turn, targeting, reward confirmation) rather than rewriting business logic.
4. **UI injection layer** — the host-menu entry, lobby add/remove player controls, and a visible current-character indicator.

## 4. Functional requirements

- Lobby: entry button on the multiplayer host menu; character select supports multiple local players, each with independent pick and ready state; start conditions match online play.
- Combat: next/previous switch hotkeys; after a switch, hand view, potion bar, character status, and end-turn all point at the new character; never break the action queue ordering. "More clicks" is acceptable; state corruption or deadlock is not.
- Non-combat: rewards/shop purchases/card removal settle to the currently controlled character; events (shared and personal) reuse online logic with switching standing in for remote input; rest sites, chests, and map selection ride the multiplayer synchronizers.
- Saves: per-character resources and history persist; after loading, player list and ownership stay intact.

## 5. Non-functional requirements

- Idempotent control switching; no heavy reflection or large allocations on hot paths.
- Observability: `[LocalMultiControl]` log prefix covering session creation, player add/remove, control switches, and key choice submissions.

## 6. Confirmed decisions (2026-03-12)

- Initial party cap: 2 (later raised to 12); duplicate characters allowed.
- Default hotkeys `[` / `]` (later superseded by `Tab` / `Shift+Tab` in v1.31).
- Standard mode first; Daily/Custom later (Custom landed 2026-03-25).
- Shared-event votes: auto-vote rather than requiring a manual switch per character.

## 7. Requirement change (2026-03-18) — per-character independence

Superseding the earlier auto-vote-everything approach:

- Events default to **per-character independent choices** (finish one character, switch to the next), with a "sync to all" toggle (default off) restoring the old behavior.
- Rest sites: each character chooses independently; full option set preserved.
- Combat loot: gold/cards/potions granted and chosen independently per character; no more mirror-copying of gold/relics; potions no longer pinned to slot 1; no extra potion slots.
- Treasure rooms keep the "5+ players copy slot 1's pick" special case.

The implementation staging for this change is in `rewards-refactor-plan.md`; it shipped around v1.17.
