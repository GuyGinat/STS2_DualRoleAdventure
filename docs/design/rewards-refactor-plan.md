# Refactor Plan: Per-Character Events / Rest Sites / Loot (translated)

> Historical planning document (2026-03-18, branch `codex/refactor-role-independent-flow`), translated from Chinese. The plan was implemented around v1.17 (2026-03-28); see `CHANGELOG.md`. Kept for design rationale.

## 1. Goal

On top of the existing local multi-control, adopt the full "independent decisions per character" semantics of real multiplayer for events, rest rooms, and post-combat loot — eliminating "one character chooses, everyone syncs".

## 2. Requirements

1. Events default to per-character independent choice; flow = current character confirms → switch to next.
2. A "sync to all" toggle at event trigger time (default **off**); only when on does the old all-sync strategy apply.
3. Rest sites: independent choice per character, keeping the original multiplayer option set.
4. Post-combat loot: independent per character (gold, cards, potions all unsynced).
5. Gold and relics are no longer copied; potions no longer go only to slot 1; no extra +2 potion slots.
6. Treasure rooms keep the existing "5+ players" special case (copy slot 1's pick).

## 3. Constraints & compatibility

- Reuse the multiplayer synchronizers and command chains; don't rewrite business flows.
- Default flip: event "sync to all" goes from implicitly-on to off-by-default (toggle-gated).
- Save compatibility: the new toggle defaults to `false`; existing flows get idempotency protection so repeat triggers can't scramble character order.
- Out of scope: Daily/Custom expansion; major UI work beyond the minimal toggle and status hints.

## 4. Phases

- **Phase A — events**: per-character sequential choice; auto-advance to the next character; toggle restores legacy sync. Accept: characters visibly complete one by one; default never syncs from a single choice; toggle restores old behavior.
- **Phase B — rest sites**: per-character choice with the full option set; each character settles in their own branch without shared-state clobbering.
- **Phase C — loot**: cancel the copy/mirror distribution; potion ownership follows the acting character; no duplicate or missing grants.
- **Phase D — treasure regression**: verify the 5+ player special case is unaffected by A–C.

## 5. Risks & rollback

1. Wrong switch timing in the event chain → reward popups misaligned with the controlled character. Mitigation: switch only after the event flow completes and blocking popups close.
2. Independent rewards conflicting with legacy mirror patches. Mitigation: retire mirror logic module-by-module behind quick-rollback switches.
3. Rest site and event sharing context state → duplicate submissions. Mitigation: keep all take/release-control interfaces idempotent; add key log assertions.

## 6. Logging

Unified prefix `[LocalMultiControl]`; planned key events: `event-independent-start`, `event-sync-all-toggle`, `restsite-independent-step`, `reward-independent-open`, `treasure-5plus-fallback`.
