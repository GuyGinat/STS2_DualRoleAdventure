# Dev-Console Commands (for mod testing)

Useful commands for jumping around the run flow while reproducing and verifying fixes. Sourced from the game's `Core/DevConsole/ConsoleCommands/` implementations (see decompiled `src/`).

## Most-used

| Command | Effect |
|---|---|
| `event THE_LEGENDS_WERE_TRUE` | trigger the treasure-map event |
| `event CRYSTAL_SPHERE` | trigger the Crystal Sphere divination event |
| `act 2` | jump straight to act 2 |
| `room Event` / `room Treasure` / `room RestSite` / `room Combat` | change the current room type |
| `travel` | toggle free travel on the map (jump to any node) |

## Notes

- `event <ID>` takes the uppercase-underscore event ID (e.g. `WELCOME_TO_WONGOS`), not a display name.
- `act <n>` is the fastest regression-test entry point.
- A full ID reference table (all `card` and `event` codes, extracted from `Core/Models`) is archived in Chinese at `docs/archive/console-id-reference.zh.md`; the IDs themselves are English and can be used directly.

## Typical mod-test sequence

1. `act 2`
2. `event THE_LEGENDS_WERE_TRUE` (acquire the treasure map)
3. Proceed to a treasure node and verify per-character settlement (each character loses the map and gains the gold).
