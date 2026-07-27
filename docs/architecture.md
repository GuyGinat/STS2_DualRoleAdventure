# Architecture — How LocalMultiControl Works

Quick orientation for the "online multiplayer → local multi-control" implementation. (Translated and updated from the original Chinese structure doc.)

## Goal & principle

- Reuse the game's real multiplayer pipeline in a single process to control 2–12 local characters.
- Input routing and state-synchronization safety come first; gameplay conveniences second.
- Implementation is Harmony patches over the game assembly (`sts2.dll`) — no baselib, no asset pack.

## Directory layout

- `Scripts/Entry.cs` — mod entry point (`[ModInitializer]`), registers all Harmony patches via `PatchAll`.
- `Scripts/Runtime/` — runtime state: multi-control context, control switching, loopback net service, UI helpers.
- `Scripts/Patch/` — Harmony patches grouped by scene/system (lobby, combat, map, rewards, rest sites, shops, events, specific relics).
- `Scripts/Models/`, `Scripts/Rewards/` — supporting data types.

## Key runtime objects

- `LocalLoopbackHostGameService` — implements the game's `INetHostGameService` with a **mutable NetId** and no real networking; multiplayer synchronizers believe they're in a session while all messages stay in-process. `SetCurrentSenderId()` changes who "the local player" is.
- `LocalSelfCoopContext` — the master switch: enabled state, desired player count (2–12), local player IDs, Vakuu assignments, character-select screen context.
- `LocalMultiSessionState` — per-session ordering of players and the currently controlled player.
- `LocalMultiControlRuntime` — lands a control switch: updates the net service sender, `LocalContext.NetId`, re-syncs each game synchronizer's notion of "local player", and refreshes the UI (hand, energy, potions, status strip).
- `LocalGhostHandsRuntime` / `LocalGhostHandsOverlay` — optional combat overlay mirroring backgrounded characters' hands with pooled `NCard` nodes.

## Core flow

1. The injected **Local Multi-Control** card on the multiplayer Host submenu starts a loopback session (`LocalLoopbackHostGameService`) and bootstraps N local players in the standard character-select screen.
2. The run starts through the normal multiplayer lobby path (auto-ready assists included).
3. During the run, hotkeys/UI switch the controlled character: sender ID + `LocalContext.NetId` + synchronizer local-player re-sync, then UI refresh.
4. Patches keep combat and non-combat stages consistent: reward aggregation with per-character labels, per-character events and rest sites, vote auto-completion where the game expects every player to vote.

## Invariants

- Control-switch and choice-submission paths are **idempotent** — repeat triggers must not desync ordering.
- Hot paths avoid heavy reflection and large allocations.
- Local mirror state must never contaminate authoritative game state.

## Related docs

- Build & deploy: `README.md`; agent rules: `AGENTS.md`
- History: `CHANGELOG.md`; open issues: `TODO.md`
- Original design docs: `docs/design/`
