# Player Guide — DualRoleAdventure (LocalMultiControl)

Control 2–12 characters by yourself in Slay the Spire 2's multiplayer mode, on one machine, with no network.

## Install & enable

1. Subscribe on the Steam Workshop (or place `DualRoleAdventure.dll` + `DualRoleAdventure.json` in `<game>\mods\DualRoleAdventure\`).
2. Launch the game → `Settings → Mods` → enable **多角色冒险 (DualRoleAdventure)**. Restart if prompted.

## Starting a run

1. Main menu → **Multiplayer → Host**.
2. Pick the **Local Multi-Control (单人多角色)** card (it sits next to Standard / Daily / Custom).
3. In character select:
   - **`+` / `-`** — add/remove local characters (2–12; duplicates allowed)
   - **`Tab` / `Shift+Tab`** — switch which character you're editing
   - Pick a character and ready-up for each slot, then start as usual.
4. **Custom mode** also works: entering `Custom Mode` from the multiplayer menu keeps the local multi-control flow, so you can use seeds and custom rules with multiple characters.

## Vakuu (AI auto-play)

Any character can be handed to the built-in autoplayer ("Vakuu"):

- On the character-select screen: toggle per character, or toggle **all** at once.
- Controller: `Y` toggles the highlighted character, `LT + Y` toggles everyone.
- Vakuu characters play their turns automatically; when no Vakuu character can act, control returns to you.

## During a run

- **Switch characters:** `Tab` (next) / `Shift+Tab` (previous). Legacy keys `]` `R` `/` (next) and `[` `T` (previous) still work.
- Each character owns their deck, hand, energy, gold, potions, relics, and choices. The UI (hand, energy, potion bar, status strip) follows whoever you control.
- **Combat:** play each character's turn, switching freely; end turn per character.
- **Rewards:** loot is generated per character and shown as one combined list, each entry prefixed with its owner (e.g. `[Player 2]`). Claim with the matching character.
- **Events:** by default each character resolves the event independently — the mod walks you through them one by one. Shared-event votes are auto-completed where the game requires everyone to vote.
- **Rest sites:** each character chooses in sequence (rest, upgrade, etc.).
- **Shops:** purchases and card removal are billed to the character currently in control.
- **Map:** picking the next node auto-completes the "everyone must vote" step.
- **Save & continue:** quit normally; `Multiplayer → Load` resumes the run and auto-readies all local characters.

## Ghost hands overlay (optional)

Shows your backgrounded characters' hands behind and above your active hand, so you can plan across the whole team:

- **`F8`** — toggle on/off (off by default; remembered between sessions)
- **`Ctrl+Arrows`** — move the display (hold to glide); **`Ctrl+Shift+Arrows`** — fine 4px steps
- Cards are semi-transparent, click-through, and update as characters draw/play.
- Settings persist to `%APPDATA%\SlayTheSpire2\dual_role_adventure_settings.json`; edit `ghostHandsScale` (default `0.5`) there to resize the cards.

## Hotkey reference

| Key | Context | Action |
|---|---|---|
| `Tab` / `Shift+Tab` | anywhere | switch controlled character (next / previous) |
| `[` `T` / `]` `R` `/` | anywhere | legacy switch aliases (previous / next) |
| `+` / `-` | lobby | change local character count (2–12) |
| `F8` | combat | toggle ghost hands overlay |
| `Ctrl+Arrows` (+`Shift`) | combat, overlay on | move ghost hands (fine steps with Shift) |
| `Y` / `LT+Y` | character select, controller | toggle Vakuu for one / all characters |
| `LT + D-pad` | character select, controller | count and edit-slot controls |

## Troubleshooting & feedback

- Log file: `%APPDATA%\SlayTheSpire2\logs\godot.log` — mod lines are prefixed `[LocalMultiControl]`.
- If something breaks, note the **act, room/screen, and exact steps**, then open a [GitHub issue](https://github.com/GuyGinat/STS2_DualRoleAdventure/issues) with the log attached.
- Known issues under investigation are tracked in [TODO.md](TODO.md).
