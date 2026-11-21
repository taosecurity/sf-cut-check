# Changelog

## 0.2.0

### What's new

Ship builder recipes that a ship technician offers now count as used. A recipe whose only conditions are a technician's `VendorSM_` vendor keyword (and maybe the player's level) is Reached when a technician the game reaches carries that keyword, so about 109,000 ship parts and their pieces move from Maybe to Reached. The saved scan of the official files now also keeps the records Mutagen couldn't read, so `scan --plugin` marks them CannotCheck exactly as a full scan does. The `sfcutcheck.cmd` alias from 0.1.9 is gone.

### Example

`why` shows the technician who offers a ship recipe (the plugin-reading lines that come first are left out):

    > sf-cut-check why co_SMC_Shields_Dogstar_104D_Guardian_lvl76
    ConstructibleObject 00455A:Starfield.esm co_SMC_Shields_Dogstar_104D_Guardian_lvl76
    verdict: Reached
    verdict rule ship-technician: a ship builder recipe a reached ship technician offers (its only conditions are a VendorSM_ keyword the technician carries, and maybe the player's level)
    reached from root rule start-game-quest (a quest that starts when the game starts):
        Quest 0EE9FD:Starfield.esm CF_Unique
          -links-> Npc 01539E:Starfield.esm CF_JasmineDurand
            -offers-> ConstructibleObject 00455A:Starfield.esm co_SMC_Shields_Dogstar_104D_Guardian_lvl76
    linked from 0 record(s):
    links to 6 record(s):
        checks   Keyword 1453B0:Starfield.esm VendorSM_BasicParts_Dogstar_Shields  [Reached]
        links    Keyword 29C476:Starfield.esm Category_ShipMod_Shields  [Reached]
        links    Keyword 29C480:Starfield.esm WorkbenchShipBuildingKeyword  [Reached]
        checks   000014:Starfield.esm  [no plugin read defines it]
        links    Perk 2C59DC:Starfield.esm Skill_StarshipDesign  [Reached]
        links    FormList 004555:Starfield.esm SMCSet_Shields_Dogstar_104D_Guardian_lvl76  [Reached]

### Accuracy

The results change. On the official Steam files (November 2026), 0.2.0 finds Reached 6,970,818, Maybe 22,473, DevOnly 126,675, Orphaned 488,432 and Unreferenced 51,209, with 23,316 reported records. 0.1.9 found Reached 6,861,715 and Maybe 131,576 (the other verdicts are the same), with 25,020 reported.

- **New root rule `ship-technician`.** A ship builder recipe (workbench `WorkbenchShipBuildingKeyword`) counts as Reached when every condition is either "has keyword `VendorSM_...`" (one keyword) or "the player's level is at least N", and an NPC the game reaches carries that keyword on its own record. `why` shows the technician as `-offers->`. On the official files, 602 of the 604 recipes of that shape are Reached; the other 2 (vehicle recipes in SFBGS004.esm) name a keyword no NPC carries and stay Maybe. Every one of the 109,103 changed records went from Maybe to Reached.
- Ship recipes with other conditions (a quest stage, a perk) stay Maybe, as before.
- The about 20 cells the engine loads by name or keyword still show as not used. No starting point was added for them, because neither the plugin data, the game exe nor the official scripts name any of them (RESULTS.md lists them).
- With the saved scan, `scan --plugin` still treats a mod's own ship recipes gated by an official `VendorSM_` keyword as Maybe, because the saved scan doesn't keep the official EditorIDs the rule needs. Add `--no-cache` to read the official files with the mod; the rule then applies.
- The first run after the update scans the official files again, because the rules and the saved scan's layout changed.

### Rename

- The `sfcutcheck.cmd` alias that 0.1.9 shipped is gone: run `sf-cut-check.exe`. The old `SFCUTCHECK_CACHE_DIR` environment variable still works in 0.2.0, which is the last release that reads it; use `SF_CUT_CHECK_CACHE_DIR`.

### Fixes

- The saved scan of the official files now lists the official records whose links or cell fields Mutagen couldn't read, so a `scan --plugin` run gives them CannotCheck, the same verdict as a full scan. Before, they took the tier saved for them. The saved file has a format version in its name, so a 0.1.9 file is never misread: it's simply rebuilt. A line the reader doesn't know also makes it scan again.
- README "Limits" now says how a relative `SF_CUT_CHECK_CACHE_DIR` is used (as given, from the current folder).
- AGENTS.md gives the saved scan's real size (about 700,000 lines, about 22 MB).

## 0.1.9 (2026-11-20)

### What's new

The tool has a new name, **sf-cut-check** (`sf-cut-check.exe`, repo `github.com/taosecurity/sf-cut-check`), and its results are more accurate. Interior cells now count as used only when something leads to them, form lists that the DLCs add to base-game lists now count with those lists, and `why` and `find` show each record's in-game name, in any of the game's languages with the new `--language` option.

### Example

`why` now prints the in-game name and the reason (the plugin-reading lines that come first are left out):

    > sf-cut-check why Ammo_Supernova
    Ammunition 091157:Starfield.esm Ammo_Supernova
    name: Supernova Ammo
    verdict: Orphaned
    reason: only records the game never reaches link to it
    linked from 3 record(s):
        links    Weapon 0307FE:Starfield.esm SWC_StarbornWeapon_Supernova02_lvl30  [Unreferenced]
        links    Weapon 0307FF:Starfield.esm SWC_StarbornWeapon_Supernova03_lvl56  [Unreferenced]
        links    Weapon 091151:Starfield.esm SWC_StarbornWeapon_Supernova01_lvl16  [Unreferenced]
    links to 3 record(s):
        links    Keyword 0F4AE8:Starfield.esm ObjectTypeAmmo  [Reached]
        links    Transform 03D201:Starfield.esm Inv_DefaultTransform_Z015  [Reached]
        links    Projectile 091155:Starfield.esm Projectile_Supernova  [Orphaned]

### Rename

- The command is `sf-cut-check`. For this release only, the zip also holds `sfcutcheck.cmd`, which runs `sf-cut-check.exe` with the same arguments and prints one line on stderr saying the name changed.
- The saved scan of the official files moved to `%LOCALAPPDATA%\sf-cut-check` (the old `%LOCALAPPDATA%\sfcutcheck` folder can be deleted). Its environment variable is `SF_CUT_CHECK_CACHE_DIR`; the old `SFCUTCHECK_CACHE_DIR` still works. The first run after the update scans the official files again, because the rules changed.
- report.json's `tool` field and the report page's byline say `sf-cut-check`.

### Accuracy

The results change. On the official Steam files (November 2026), 0.1.9 finds Reached 6,861,715, Maybe 131,576, DevOnly 126,675, Orphaned 488,432 and Unreferenced 51,209, with 25,020 reported records. 0.1.8 found 7,478,997, 7,357, 116,915, 5,167 and 51,171, with 23,857 reported.

- **Interior cells are no longer starting points by themselves.** One counts as used only when something leads to it: a door whose destination is inside it, a quest alias or location that names a reference inside it, its own location, a pack-in, a script property, or (for a ship's own cells) the placed ship they belong to. About 3,000 of the game's 16,707 interiors are no longer reached (cells are listed with `--all-types`), most of them pack-in storage cells. Interior test cells stay DevOnly.
- A cell the engine or a quest picks only by keyword, with no link in the data, shows as not used.
- For a cell, links from the records it holds itself (its navmeshes) no longer count, so a cell nothing outside leads to is Unreferenced.
- **Form lists with AddToList** (the DLCs use them to add ship modules, photo mode poses, help pages and outpost furniture to base-game lists) now count as used when the list they add to does. About 58,900 records, mostly ship habitat pack-ins from SFBGS006.esm, move from Orphaned to Maybe.
- A record reached through a record that is only checked in a condition now reads "via checked-only: <checked record>, checked by <checker>". Before, 28 rows on the official files named the wrong checker.
- Ship builder recipes stay Maybe: almost all of them depend on the ship technician's vendor keyword, the player's level or a quest, which the tool can't confirm from the files.

### New and changed output

- **`--language <code>`** picks the language of the in-game names in the reports, `why` and `find` (`en` by default; `de`, `es`, `fr`, `it`, `ja`, `pl`, `ptbr`, `zhhans`, or Mutagen's names such as `German`). An unknown code, or one with no string files, stops with exit code 2 and lists the languages found. report.json has a `language` field, and the report page's "What was checked" names the language. Verdicts never change with it.
- `why` prints a `name:` line (the in-game name) and a `reason:` line (the same text as the reports' reason). For a cell it lists the outside links into the references inside it first, under "links into the N record(s) inside it from outside". The step from a placed reference to its cell shows as the new link kind `in`, and the step from a list to a form list that adds to it as `owns`.
- `find` shows each record's in-game name in quotes after its EditorID, and the reason at the end of Maybe and DevOnly rows.
- `why` counts "linked from" the same way as the reports and the verdict: for a cell, the records inside it are counted separately ("plus N inside it").
- RESULTS.md lists the known engine-picked cells that show as not used.

### Fixes from the release review

- A cell whose location or linked references Mutagen can't read no longer stops the whole scan; it comes out CannotCheck, and the first few are logged.
- `--language`: names in a plugin that isn't localized aren't counted as "kept in English"; string files are found without regard to case (Linux, Proton); the Localization archive is listed first, and the other archives are opened only while the chosen language hasn't been found.
- When the old `%LOCALAPPDATA%\sfcutcheck` folder is still there, a scan that saves its result says it can be deleted.

### Download

Download `sf-cut-check-0.1.9-win-x64.zip` and its `.sha256` file below, right-click the zip, choose **Extract All...**, and run `sf-cut-check.exe` from a Command Prompt in that folder. The [README](https://github.com/taosecurity/sf-cut-check#install) has the steps.

## 0.1.8 (2026-11-16)

Speed release. The results are the same as 0.1.7: on the official files both index 7,659,607 records and
37,222,256 links and write identical reports.

- A full scan reads each placed reference once less. A record that holds others (a worldspace, a cell, a quest)
  is now compared only with the records directly inside it, and what was read there is reused when the scan
  reaches those records (code review finding 10). The first scan of the official files went from 137 s to 127 s,
  and its peak memory from 3.46 GB to 3.11 GB, on the author's PC.
- The release's `.sha256` file has a plain line ending, so `sha256sum -c` works on Linux.

## 0.1.7 (2026-11-16)

Security and attribution release.

- A plugin whose master list holds a path instead of a file name (a network path such as `\\server\share\x.esm`,
  a drive or `..`) is refused with a clear message. Before this, checking such a crafted mod could make Windows
  contact another computer and send the user's login hash. Built on BuildKit 0.3.1, which has the same check.
- Text from plugins (EditorIDs, names) is printed with control characters shown as `\xNN`, so a mod can't hide
  terminal escape codes in it.
- The tool says who made it: one line on stderr at the start of each run, `--version` with credits, a byline and
  credits on the report page, and the author in report.json and the exe's file properties.

## 0.1.6 (2026-11-11)

First version.

- `scan` reads the official plugins, walks from the game's starting points (start-game quests, the story manager,
  worlds and cells, default objects, game settings, Gameplay Options groups, stars and planets, the player) and
  reports what nothing reaches: Maybe, DevOnly, Orphaned, Unreferenced and CannotCheck, each with its reason.
- `scan --plugin <file>` reports the records a mod adds that nothing reaches (exit 1 if any). The official files are
  scanned once and the result saved, so later checks take seconds until the game files change (`--no-cache` to
  scan again).
- Every report says what was checked: the target, the game folder, whether the official files were read or came
  from the saved scan, the plugins read, what's shown, the result, and the version and date.
- RESULTS.md and a panel at the top of the report explain each verdict in plain words, with a real example, what
  it means for players and how to use cut content in a mod.
- Attribution: a byline at the start of each run (stderr) and in `--version`, a byline and credits footer on the
  report page, `author`, `license` and `url` in report.json, and the author and copyright in the exe's file properties.
- `why` prints the path from a starting point to a record, or what links to it.
- `find` lists records by EditorID text with their verdicts.
- Reports: CSV, JSON and a self-contained HTML page with filters. In-game names come from the string tables.
- Links found only in conditions are "checks": a check on content (a quest, item, NPC, spell...) gives Maybe, a check
  on logic data (a condition record, global, form list...) reaches it.
- `--verbose` and `--log <file>` give a troubleshooting log; errors name the file and the fix.
- `--masters-dir` for masters outside the Data folder (Mod Organizer 2). Game Pass's usual Data folder is found when
  Steam's isn't there.
