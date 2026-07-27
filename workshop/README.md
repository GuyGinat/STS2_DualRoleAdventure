# Steam Workshop Upload Workspace

Working folder for publishing this mod to the Steam Workshop (appid `2868840`).

## Layout

- `content/` — the files actually uploaded: `DualRoleAdventure.dll` + `DualRoleAdventure.json`. Overwrite with the latest release build before each upload.
- `preview.jpg` — the item's preview image.
- `steamcmd_item_fork.vdf` — upload config for **this fork's** Workshop item (maintained by GuyGinat). After the first upload SteamCMD writes the assigned `publishedfileid` back into this file — commit that change.
- `steamcmd_item.vdf` / `mod_id.txt` / `workshop.json` / `image.png` — the **original author's** upload workspace for item [3747538947](https://steamcommunity.com/sharedfiles/filedetails/?id=3747538947), kept for reference. Never upload with the original vdf.

## Upload procedure (maintainer)

1. Follow the release flow in `AGENTS.md` §7 (version bump, changelog, Release build, restage `content/`).
2. Update `changenote` in `steamcmd_item_fork.vdf`.
3. Run (Steam Guard prompt on first login):

```
steamcmd +login <steam_username> +workshop_build_item "<absolute path>\workshop\steamcmd_item_fork.vdf" +quit
```

4. First upload only: the vdf ships with `visibility "2"` (private) — verify the item page, accept the Workshop agreement if prompted, subscribe to confirm the game loads it, then set visibility to Public on the item page and change the vdf to `"0"` for future updates.
