# AGENTS.md — LocalMultiControl Collaboration Rules

Rules for automated coding agents (and humans) working in this repository. Goal: changes that are stable, verifiable, and easy to roll back.

## 1. Scope & hard constraints

- Modify only mod code and mod metadata: `Scripts/`, `*.csproj`, `*.json`, docs, `workshop/`.
- `src/` is decompiled game source — **read-only reference, never committed** (gitignored). Regenerate it after each game patch (see §5).
- No destructive git operations (`reset --hard`, force-push, `checkout --` over user changes). Never push to `origin` (the original author's repo); pushes go to `fork`.
- Language: **English** for all new code comments, commits, logs, and documentation. Original Chinese documents are preserved under `docs/archive/`.
- Commit after each logical change with a clear message.

## 2. Build, format, deploy

Run from the repo root:

```bash
dotnet restore LocalMultiControl.csproj
dotnet build LocalMultiControl.csproj -c Debug     # or -c Release for shipping
dotnet format LocalMultiControl.csproj --verify-no-changes
```

- The build copies the DLL to the repo root: `DualRoleAdventure.dll`. **Always deploy/ship the root artifact**, not `.godot/mono/temp/...`.
- Deploy = copy `DualRoleAdventure.dll` + `DualRoleAdventure.json` to `<game>\mods\DualRoleAdventure\` (`copy_pck_to_game.ps1`, or plain copy). No pck export — this is a dll-only mod.
- If the copy fails with *permission denied*, the game is running and holds the DLL lock; retry after it closes.

## 3. Runtime verification

- Log file: `%APPDATA%\SlayTheSpire2\logs\godot.log`.
- Log via `Log.Info` with the unified prefix `[LocalMultiControl]` (`Log.Debug` is invisible by default). Add logs for anything you fix.
- On startup the mod logs `开始初始化 Harmony 补丁` → build marker → `Mod 初始化完成`; any Harmony exception between those lines means a patch target broke.
- There are no automated tests; the maintainer playtests. Provide focused, step-by-step test scripts and read the log after each round.

## 4. Harmony & domain conventions

- Prefer `Postfix` for added behavior, `Prefix` for guards; keep hot-path patches lightweight (no heavy reflection or allocation per frame).
- Control-switch and choice-submission paths must be **idempotent** — repeated triggers must not corrupt ordering or state.
- Never let local mirror/UI state pollute the authoritative game state (run state, piles, synchronizers).
- Patch naming: `PrefixXxx` / `PostfixXxx`; file per game type/scene under `Scripts/Patch/`.

## 5. Game-patch adaptation playbook

When the game updates and the mod breaks:

1. Note the new version from `<game>\release_info.json`.
2. Regenerate decompiled reference source:
   ```bash
   dotnet tool install -g ilspycmd --version 9.1.0.7988   # newer majors may fail to install
   ilspycmd -p --nested-directories -o ~/sts2-src "<game>/data_sts2_windows_x86_64/sts2.dll"
   cp -r ~/sts2-src/MegaCrit/Sts2/. src/
   ```
3. Build; fix compile errors using the decompiled source as ground truth (compile errors = renamed/removed members).
4. Validate every **string-based** Harmony/`AccessTools` target against the decompiled tree — these fail at runtime, not compile time.
5. Fix pattern: call the **new** member name first, with a reflection fallback to the old name (see `InvokeBeginRunIfAllPlayersReady` in `Scripts/Patch/LoadRunLobbyPatch.cs`).
6. Record every fixed breakage in `CHANGELOG.md`.

## 6. Code style

- `using` order: system → third-party → project namespaces; remove unused.
- File-scoped namespaces: `namespace LocalMultiControl.Scripts.Patch;`
- Types/methods/properties `PascalCase`; locals/params `camelCase`; private fields `_camelCase`.
- Explicit types over `var`; `<Nullable>enable</Nullable>` — handle null branches.
- Custom Godot node subclasses must be `partial` (source generators).

## 7. Release flow

1. Bump the version: `DualRoleAdventure.json` (`x.y.z` semver — the game warns on non-semver), `mod_manifest.json`, Workshop title `Vx.xx`, `Entry.cs` build marker.
2. Update `CHANGELOG.md` (cut a dated release section) and `PLAYER_GUIDE.md` if player-facing behavior changed.
3. `dotnet build -c Release`; copy `DualRoleAdventure.dll` + `DualRoleAdventure.json` into `workshop/content/`.
4. Update `workshop/steamcmd_item_fork.vdf` (`changenote`; `publishedfileid` stays once assigned). The **maintainer** runs the SteamCMD upload — it needs their Steam login.
5. Commit, push to `fork`, optionally create a GitHub release (zip via `Scripts/Tools/BuildRelease.ps1`).
6. Never touch the original author's Workshop item (3747538947).

## 8. Documentation map

- `README.md` — project front door; `PLAYER_GUIDE.md` — player-facing usage; `CHANGELOG.md` — history; `TODO.md` — open issues.
- `docs/architecture.md`, `docs/console-commands.md`, `docs/design/*` — developer docs.
- `docs/archive/*.zh.md` — original Chinese documents, preserved verbatim; do not edit them.
