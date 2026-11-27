# sf-cut-check

sf-cut-check finds what Starfield never uses. It reads the game's own plugins and tells you which records the player
can never meet (cut weapons, quests nothing starts, dialogue nobody says, items in lists nothing hands out), and for
each one it shows *why*, with the chain of records behind the answer, not a made-up percentage.

Mod authors can point it at their own plugin to find records the mod adds but never uses: leftovers from editing that
only make the plugin bigger. It only reads. It never changes a plugin or any game file, and it never connects to the
internet.

Download the latest zip from [Releases](https://github.com/taosecurity/sf-cut-check/releases); [Install](#install)
has the steps.

## First command

With Starfield installed through Steam in the usual place, open a Command Prompt in the folder that holds
`sf-cut-check.exe` (see [Install](#install)) and run:

    sf-cut-check scan

It takes about 2 to 3 minutes and needs about 3 GB of free memory. When it's done, open `out\scan\report.html` in
your browser. A summary is printed in the terminal as well. If your game is somewhere else, add `--data` with its
`Data` folder (see [Where the game is](#where-the-game-is)).

For AI assistants: step-by-step instructions are in [AGENTS.md](AGENTS.md).

## Example

A full scan of the Steam version (November 2026), trimmed (`...` marks lines left out):

    > sf-cut-check scan
    reading 18 plugins from C:\Program Files (x86)\Steam\steamapps\common\Starfield\Data (about 2 minutes for the whole official set)...
        75.1s Starfield.esm: 3,829,246 records, 14,126,274 links
        86.9s BlueprintShips-Starfield.esm: 1,503,332 records, 9,518,206 links
        ...
       128.3s walk done
    records: 7,659,607; links: 41,810,130; roots: 13,794
        root rule start-game-quest   (Strong)      381  a quest that starts when the game starts
        root rule story-manager      (Strong)       20  a story manager event node (the game runs these on events)
        root rule world              (Strong)      554  a worldspace that isn't developer content
        ...
        root rule ship-technician    (Strong)      558  a ship builder recipe a reached ship technician offers (its only conditions are a VendorSM_ keyword the technician carries, and maybe the player's level)

    all records by verdict:
        Reached        6,970,818
        Maybe             22,473
        DevOnly          126,675
        Orphaned         488,432
        Unreferenced      51,209

    reported (not Reached, content types only): 23,316
        type                            Maybe  DevOnly  Orphaned    Unref  Cannot
        Static                            107      223       301    2,583       0
        ConstructibleObject             2,840        0         0        0       0
        PackIn                            326      109       346    1,896       0
        Npc                                56        9       715      478       0
        ...
        ... and 105 more types (5,330 records): see report.html

    reports:
        ...\out\scan\report.csv
        ...\out\scan\report.json
        ...\out\scan\report.html
    time: 130 s

And checking one mod, the second time (the official scan is saved after the first):

    > sf-cut-check scan --plugin MyMod.esm
    Checking MyMod.esm against the saved scan of the 18 official files (made 2026-11-11 22:14 UTC;
    your game files haven't changed since). Reading only the mod. Add --no-cache to scan the official files again.
    ...
    RESULT: MyMod.esm adds 28 records, and the game reaches all of them. 12 are reached only through a recipe,
    research project or condition check, so they're marked Maybe. None is reached only from test cells.

**New name since 0.1.9.** The tool was called `sfcutcheck` up to 0.1.8. The `sfcutcheck.cmd` alias that 0.1.9
shipped is gone in 0.2.0, so scripts must run `sf-cut-check.exe`.

## Install

**Stand-alone exe (Windows 10 or 11, x64):**

1. Download `sf-cut-check-0.2.1-win-x64.zip` and its `.sha256` file from the
   [releases page](https://github.com/taosecurity/sf-cut-check/releases).
2. Right-click the zip, choose **Extract All...**, and pick a folder, for example `C:\Tools\sf-cut-check`.
3. Open a command prompt in that folder: open the folder in File Explorer, click the address bar at the top, type
   `cmd` and press Enter. A Command Prompt window opens in that folder, ready for the commands below.
4. Check that it runs:

       sf-cut-check --version

Nothing else is needed: .NET is built into the exe.

To check that the zip is the one that was published, run this in the folder you downloaded it to, and compare the
hash it prints with the one in the `.sha256` file:

    certutil -hashfile sf-cut-check-0.2.1-win-x64.zip SHA256

**The exe isn't code-signed.** The first time you run it, Windows SmartScreen may show "Windows protected your PC".
Click **More info**, then **Run anyway**. Some antivirus tools are also wary of unsigned single-file .NET programs,
because that packaging unpacks itself when it runs. To be sure you have the file that was published, compare the
`certutil` hash above with the `.sha256` file on the release page. Or skip the exe and build it yourself from the
source (below).

**From source (Windows or Linux, .NET SDK 9.0 or later):**

    git clone https://github.com/taosecurity/sf-cut-check
    cd sf-cut-check
    dotnet build src -c Release
    dotnet test tests/SfCutCheck.Tests

The program is then `src/bin/Release/net9.0/sf-cut-check` (`.exe` on Windows).

## Words used here

- **Plugin:** a mod's data file (`.esm`, `.esp` or `.esl`). It adds or changes records in the game.
- **Master:** a plugin that another plugin needs loaded before it. Starfield.esm is the master of nearly every mod.
- **Record:** one thing in a plugin, such as a weapon, a quest, a location or a setting.
- **FormID:** a record's number, such as `091157`. The first two digits of the full number depend on the load order.
- **EditorID:** a record's readable name, such as `Ammo_Supernova`, used by the Creation Kit and xEdit.
- **Override:** a later plugin's copy of a record from an earlier one, which replaces it in the game.
- **Leveled list:** a list the game picks from when it hands out loot, places an enemy or stocks a vendor. An item
  in a leveled list that nothing uses is never given out.
- **Pack-in:** a ready-made group of objects (a shelf with its clutter, a ship habitat's furniture) that is placed in
  the world as one piece.
- **Cell:** one area of the game. An interior cell is an indoor space, such as a building, a cave or a ship's inside.
- **Mutagen:** a .NET library for reading and writing Bethesda plugins, which sf-cut-check uses.

## What it tells you

**[RESULTS.md](RESULTS.md) explains every verdict in plain words**, with a real Starfield example of each, what it
means for players, and how a mod author can use cut content. The report page has a short version at the top.

Each reported record gets one verdict:

| Verdict | Meaning |
|---|---|
| **Maybe** | Reached only in a way a file reader can't confirm: through a crafting or building recipe, a research project, a record the engine picks by its conditions, or a condition that *checks* the record without anything starting or giving it. |
| **DevOnly** | Only Bethesda's test and debug cells lead to it (cells named with words like `QA`, `Test`, `Debug`, `zz`). |
| **Orphaned** | Something links to it, but only records the game itself never reaches (for example an item in a leveled list no one uses). |
| **Unreferenced** | Nothing links to it at all. |
| **CannotCheck** | The plugin reader couldn't read this record's links. sf-cut-check never calls these cut. |

Records the game does reach aren't listed (they're counted in the summary). Structure records, such as placed
references, navmeshes and keywords, are left out unless you ask for `--all-types`.

The **starting points** are the places the game begins using content by itself: quests that start with the game, the
story manager's event nodes, every worldspace that isn't a test world, default objects, game settings, Gameplay
Options groups, stars and planets, and the player. Everything sf-cut-check can reach from them by following links counts
as used. The report page lists every rule and how many starting points it found.

An **interior cell** isn't a starting point by itself. It counts as used only when something leads to it: a door
whose destination is inside it, a quest alias or location that names a reference inside it, its own location, or a
direct link (a script property, a pack-in, a ship). A ship's own interior and exterior cells count when the placed
ship they belong to does.

In `why`, the step from a reference to the cell it's in shows as `in`, and for a cell `why` also lists the records
outside it that link to the references inside it (a door leading in, for example). Links from records inside a cell
(its navmeshes) don't count for it, so a cell nothing outside leads to is Unreferenced. Interior test cells
(EditorIDs such as `zzTestCell` or `QATestCell02`) stay starting points for DevOnly.

A **form list with AddToList** (the DLCs use these to add ship modules, photo mode poses, help pages and outpost
furniture to base-game lists) joins the list it names, so it counts as used when that list does. In `why` the step
from the named list to it shows as `owns`.

A **hint** is shown when an EditorID carries a Bethesda marker such as `CUT`, `zz`, `Unused`, `DoNotUse` or
`DUPLICATE`. It's a clue, not a verdict.

## Commands

    sf-cut-check scan  [--data <Data folder>] [--types Weapon,Armor] [--all-types] [--out <folder>]
    sf-cut-check scan  --plugin <MyMod.esm or full path> [--masters-dir <folder>]... [--out <folder>]
    sf-cut-check why   <FormID:Plugin.esm or EditorID> [more...]
    sf-cut-check find  <text> [more...] [--word] [--types Weapon,Armor] [--max 40]
    sf-cut-check --version

Every command also takes `--data <Data folder>`, `--plugins a.esm,b.esm` (read these plugins instead of the official
set), `--language <code>`, `--verbose` and `--log <file>`.

`--language <code>` picks the language of the in-game names in the reports, `why` and `find` (English, `en`, when
it isn't given). The codes are the ones in the game's string file names: `de`, `es`, `fr`, `it`, `ja`, `pl`, `ptbr`,
`zhhans` and `en` (Mutagen's names, such as `German`, work too). Steam's copy holds every language in
`Starfield - Localization.ba2`, whatever language the game is set to.

A plugin with no text in that language keeps its English names, and the console and the report page say which
plugins did. A code the tool doesn't know, or one with no string files for Starfield.esm, stops with exit code 2 and
lists the languages that were found. Verdicts never depend on the language. See `find` below for an example.

Each command reads the plugins again, which takes the same 2 to 3 minutes. `why` and `find` take several records at
once, so ask about all of them in one run.

### scan

`scan` reads the official plugins (Starfield.esm, the DLC and Bethesda's creations found in the Data folder) and
writes the reports. `--types` limits the report to some record types (in PowerShell, quote the list:
`--types "Weapon,Armor"`). Its output is shown in [Example](#example).

### scan --plugin

`scan --plugin` is for mod authors. It reports only the records your plugin *adds*, ends with a `RESULT:` line, and
exits with 1 if any of its content records (or the record types you chose with `--types` or `--all-types`) are Orphaned or
Unreferenced, so you can use it in a build script. If the run leaves out records that nothing reaches (structure such
as placed references, or types outside `--types`), the `RESULT:` line names how many. The second run in
[Example](#example) shows its output.

**The whole game is scanned too, even though you named one plugin.** A mod's records count as used only if the game
can reach them, and that usually happens through Bethesda's own records: a leveled list your mod adds an item to, a
vanilla quest, a cell you place something in. So the first `--plugin` run scans the official files (2 to 3 minutes)
and saves the result. Later runs, for any mod, read only your mod and its masters and take a few seconds.

The console says which happened. The saved scan is used only while your official files are unchanged: after a
Starfield update, or when you add or remove a Creation, the next run scans again by itself. `--no-cache` reads the
official files for that run only and doesn't save them; a plain `sf-cut-check scan` rebuilds the saved scan.
The saved scan lives in `%LOCALAPPDATA%\sf-cut-check`, or in the folder named by the `SF_CUT_CHECK_CACHE_DIR` environment variable (the old name `SFCUTCHECK_CACHE_DIR` still
works in 0.2.0, its last release). Up to 0.1.8 it was in
`%LOCALAPPDATA%\sfcutcheck` (the old name), which can be deleted.

### why

`why` explains one record: its in-game name, its reason, the path from a starting point to it, or the records that
link to it and their verdicts. Each link shows its kind; `checks` means the link is only in a condition. Give an
EditorID, or a FormID as `FormID:Plugin.esm`:

    sf-cut-check why CUT_Skill_Mindfulness
    sf-cut-check why 0DF320:Starfield.esm

Here is the answer for the Starborn Supernova ammo, without the plugin-reading lines that come first:

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

### find

`find` lists records whose EditorID contains some text, with their in-game names and verdicts (and the reason for
Maybe and DevOnly rows). `--word` matches whole words only (`--word cut` finds `CUT_AmmoMag` but not
`CutsceneIntro`; it matches a run of whole words, so `--word SamCoe` finds `COM_Sam_Coe_Neon01`; digits count as a word of their own, so `--word Neon02` doesn't find it), and `--max` sets how many rows it lists for each text.

Here it is with German names and only three rows, trimmed:

    > sf-cut-check find Supernova --language de --max 3
    sf-cut-check 0.2.1 by Richard Bejtlich - GPL-3.0 - https://github.com/taosecurity/sf-cut-check
    reading 18 plugins from C:\Program Files (x86)\Steam\steamapps\common\Starfield\Data (about 2 minutes for the whole official set)...
    ...
      note: some in-game names stay in English, because these plugins have no German text for them: Starfield.esm (414), ShatteredSpace.esm (5), SFBGS003.esm (1), SFBGS004.esm (1), sfta05.esm (1)
       122.8s walk done
    === "Supernova": 49 records; Reached 22, Maybe 10, Orphaned 11, Unreferenced 6
        Reached      Spell                  23AF03:Starfield.esm         AIPower_Supernova "Supernova"
        Maybe        ConstructibleObject    26AFE1:Starfield.esm         co_SMC_Engine_Reladyne_Supernova_2000  (via recipe: ConstructibleObject 26AFE1:Starfield.esm co_SMC_Engine_Reladyne_Supernova_2000)
        Orphaned     Ammunition             091157:Starfield.esm         Ammo_Supernova "Supernova-Munition"
        ... 46 more (raise --max, or narrow with --types)

### spike

`spike` prints diagnostics only: how many records and links were read, the time and memory used, and the records
the plugin reader couldn't read (`--skip Type,Type` leaves record types out).

## Where the game is

sf-cut-check needs the game's `Data` folder, the one that holds `Starfield.esm`.

| Install | Data folder |
|---|---|
| Steam (default) | `C:\Program Files (x86)\Steam\steamapps\common\Starfield\Data` (found by itself) |
| Steam, another library | `<library>\steamapps\common\Starfield\Data` (usually found by itself through Steam's library list; if not, give it with `--data`) |
| Game Pass / Xbox app | `<drive>:\XboxGames\Starfield\Content\Data` (found by itself when Steam's folder isn't there) |
| Vortex | the game's own Data folder: Vortex links mods into it, so `--plugin MyMod.esm` works as is |
| Mod Organizer 2 | the game's own Data folder for `--data`; give `--plugin` the full path of the plugin in its MO2 mod folder, and `--masters-dir <folder>` for each mod folder that holds one of its masters |

## Reports

`scan` writes three files to `--out` (`out\scan` under the current folder by default):

- **`report.html`**: open it in any browser. Click a verdict to filter, pick a record type or hint, or search by
  EditorID, name, FormID or reason. It works offline and shows 500 rows at a time.
- **`report.csv`**: one row per record, for Excel or a script.
- **`report.json`**: the same rows, plus the run's settings, the starting-point rules and the counts.

The reports list FormIDs, EditorIDs and in-game names from the game's plugins. They're made on your computer from your
own copy of the game.

## Troubleshooting log

Add `--verbose` to see log lines in the terminal (each starts with `log:`), or `--log <file>` to write them to a file
with times:

    sf-cut-check scan --log C:\Temp\sf-cut-check.log

The log records the version and the arguments, which Data folder was used and why, each plugin read (size and date),
the record and link counts, any records the plugin reader failed on, the starting points per rule, the report files
written, and the full error if the run stops. Attach it to a bug report.

Exit codes: **0** done; **1** `why` couldn't find a record, or `scan --plugin` found content records nothing reaches (the types chosen with `--types` or `--all-types`, when given); **2**
couldn't run (an unknown option, an option given without its value, no `Starfield.esm` in the Data folder, a folder it can't write to, a plugin or master
it can't find). Every error message says what to change.

## Limits

- **Scripts aren't read.** A record that only a script fetches by its ID (`Game.GetFormFromFile`, forms found at run
  time) looks unused. Script *properties* are read, so most scripted content is found. When a script is the only
  plausible way something is given, the record is usually Maybe rather than Orphaned.
- **Every scene and topic inside a running quest counts as used**, even ones whose conditions can never be met.
- **Interior cells are reached only through links.** An interior that only a script loads by its ID, or only the
  ship builder or an outpost uses, comes out Orphaned or Maybe.
- **`scan --plugin` doesn't apply rule `ship-technician` when it uses the saved scan.** The saved scan keeps the
  official records' verdicts but not their EditorIDs, so the tool can't tell that a mod's recipe uses the official
  ship builder workbench and a `VendorSM_` keyword. Such a recipe stays Maybe in that mode, even when the mod adds
  its own technician. Add `--no-cache` (`scan --plugin MyMod.esm --no-cache`) to read the official files together
  with the mod (2 to 3 minutes each time, because `--no-cache` never saves the scan): the rule then applies, and
  the report still lists only the mod's records.
- **Overrides are combined.** Links from every version of a record count, so a record a later plugin stops using
  still counts as used.
- It reads plugins with Mutagen.Bethesda.Starfield 0.54.4. A handful of official records can't be read (they show as
  CannotCheck or are left out of the walk). [MUTAGEN-GAPS.md](MUTAGEN-GAPS.md) lists them.
- **The saved scan's folder can be relative.** When `SF_CUT_CHECK_CACHE_DIR` holds a relative path, it's used as
  given, so it depends on the folder you run the tool from. With no `%LOCALAPPDATA%` (for example Linux with no
  `HOME`) and no cache variable, the saved scan goes to an `sf-cut-check` folder inside the current folder. Set the
  variable to a full path to avoid both.
- Starfield only.

## Found a mistake?

Open an issue: https://github.com/taosecurity/sf-cut-check/issues

## Credits and license

sf-cut-check is made by Richard Bejtlich. It was inspired by georgearhitektive's
[Bethesda Archaeology Studio Cut Content Analyzer](https://www.nexusmods.com/starfield/mods/17708)
([source](https://github.com/georgearhitektive/BethesdaArchaeologyStudioCutContentAnalyzerBETA)), the first tool to
try to list cut content straight from Bethesda's plugins. Thanks to its author for the idea.

sf-cut-check is original work. **No code, data or design was taken from it:** it was written from scratch on top of
the [Mutagen](https://github.com/Mutagen-Modding/Mutagen) plugin library, and it works differently.
- It follows every kind of link the game uses (placed objects, leveled lists, script properties, conditions,
  dialogue, the story manager) and walks from the game's own starting points. It doesn't flag records that nothing
  points at.
- It gives each verdict with its evidence, instead of a confidence percentage.
- It's Starfield only, and it adds a check for mod authors (`scan --plugin`).

The license is GPL-3.0-only (see [LICENSE](LICENSE) and [NOTICE](NOTICE)). Privacy: [PRIVACY.md](PRIVACY.md), which says it sends
nothing anywhere.
