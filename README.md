# sf-cut-check

**Find what Starfield never uses.** sf-cut-check reads the game's own plugins and tells you which records the player
can never meet: cut weapons, quests nothing starts, dialogue nobody says, items in lists nothing hands out. For each
one it shows *why* it thinks so, with the chain of records behind the answer, not a made-up percentage.

Mod authors can point it at their own plugin to find records the mod adds but never uses: leftovers from editing that
only make the plugin bigger.

It only reads. It never changes a plugin or any game file, and it never connects to the internet.

**For AI assistants:** step-by-step instructions are in [AGENTS.md](AGENTS.md).

## First command

With Starfield installed through Steam in the usual place, run this in a terminal:

    sf-cut-check scan

It takes about 2 to 3 minutes and needs about 3 GB of free memory. When it's done, open `out\scan\report.html` in
your browser. A summary is printed in the terminal as well.

If your game is somewhere else, add `--data` with its `Data` folder (see [Where the game is](#where-the-game-is)).

### What a scan looks like

A full scan of the Steam version (November 2026), trimmed (`...` marks lines left out):

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

    all records by verdict:
        Reached        6,861,715
        Maybe            131,576
        DevOnly          126,675
        Orphaned         488,432
        Unreferenced      51,209

    reported (not Reached, content types only): 25,020
        type                            Maybe  DevOnly  Orphaned    Unref  Cannot
        ConstructibleObject             3,398        0         0        0       0
        Static                            121      223       301    2,583       0
        PackIn                            813      109       346    1,896       0
        Npc                                56        9       715      478       0
        ...
        ... and 105 more types (5,354 records): see report.html

    reports:
        ...\out\scan\report.csv
        ...\out\scan\report.json
        ...\out\scan\report.html
    time: 130 s

And checking one mod, the second time (the official scan is saved after the first):

    Checking MyMod.esm against the saved scan of the 18 official files (made 2026-11-11 22:14 UTC;
    your game files haven't changed since). Reading only the mod. Add --no-cache to scan the official files again.
    ...
    RESULT: MyMod.esm adds 28 records, and the game reaches all of them. 12 are reached only through a recipe,
    research project or condition check, so they're marked Maybe. None is reached only from test cells.

## Install

**Stand-alone exe (Windows 10 or 11, x64):** download `sf-cut-check-0.1.9-win-x64.zip` and its `.sha256` file from the
[releases page](https://github.com/taosecurity/sf-cut-check/releases), check the checksum, and unzip it anywhere:

    certutil -hashfile sf-cut-check-0.1.9-win-x64.zip SHA256
    mkdir C:\Tools\sf-cut-check
    tar -xf sf-cut-check-0.1.9-win-x64.zip -C C:\Tools\sf-cut-check
    C:\Tools\sf-cut-check\sf-cut-check.exe --version

Nothing else is needed: .NET is built into the exe.

**New name in 0.1.9.** The tool was called `sfcutcheck` up to 0.1.8. The zip still holds a small `sfcutcheck.cmd`
that runs `sf-cut-check.exe` with the same arguments (and says the name changed), so old scripts keep working for
this release; it goes away in the next one. The saved scan moved from `%LOCALAPPDATA%\sfcutcheck` to
`%LOCALAPPDATA%\sf-cut-check` (the old folder can be deleted), and its environment variable is now
`SF_CUT_CHECK_CACHE_DIR` (`SFCUTCHECK_CACHE_DIR` still works).

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
ship they belong to does. In `why`, the step from a reference to the cell it's in shows as `in`, and for a cell `why`
also lists the records outside it that link to the references inside it (a door leading in, for example). Links
from records inside a cell (its navmeshes) don't count for it, so a cell nothing outside leads to is Unreferenced.
Interior test cells (EditorIDs such as `zzTestCell` or `QATestCell02`) stay starting points for DevOnly.

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
`Starfield - Localization.ba2`, whatever language the game is set to. A plugin with no text in that language keeps
its English names, and the console and the report page say which plugins did. A code the tool doesn't know, or one
with no string files for Starfield.esm, stops with exit code 2 and lists the languages that were found. Verdicts never
depend on the language.

    sf-cut-check find Supernova --language de

- **`scan`** reads the official plugins (Starfield.esm, the DLC and Bethesda's creations found in the Data folder) and
  writes the reports. `--types` limits the report to some record types (in PowerShell, quote the list:
  `--types "Weapon,Armor"`).
- **`scan --plugin`** is for mod authors. It reports only the records your plugin *adds*, ends with a `RESULT:` line,
  and exits with 1 if any of them are Orphaned or Unreferenced, so you can use it in a build script.

  **The whole game is scanned too, even though you named one plugin.** A mod's records count as used only if the game
  can reach them, and that usually happens through Bethesda's own records: a leveled list your mod adds an item to, a
  vanilla quest, a cell you place something in. So the first `--plugin` run scans the official files (2 to 3
  minutes) and saves the result. Later runs, for any mod, read only your mod and its masters and take a few seconds.
  The console says which happened. The saved scan is used only while your official files are unchanged: after a
  Starfield update, or when you add or remove a Creation, the next run scans again by itself. `--no-cache` forces a
  fresh scan. A plain `sf-cut-check scan` also saves it.
- **`why`** explains one record: its in-game name, its reason, the path from a starting point to it, or the records that link to it
  and their verdicts. Each link shows its kind; `checks` means the link is only in a condition.

      sf-cut-check why CUT_Skill_Mindfulness
      sf-cut-check why 0DF320:Starfield.esm

- **`find`** lists records whose EditorID contains some text, with their in-game names and verdicts (and the reason for Maybe and
  DevOnly rows). `--word` matches whole words only
  (`--word cut` finds `CUT_AmmoMag` but not `CutsceneIntro`).

      sf-cut-check find Flamethrower Trident --max 40

- **`spike`** prints diagnostics only: how many records and links were read, the time and memory used, and the
  records the plugin reader couldn't read (`--skip Type,Type` leaves record types out).

Each command reads the plugins again, which takes the same 2 to 3 minutes. `why` and `find` take several records at
once, so ask about all of them in one run.

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

Exit codes: **0** done; **1** `why` couldn't find a record, or `scan --plugin` found records nothing reaches; **2**
couldn't run (an unknown option, no `Starfield.esm` in the Data folder, a folder it can't write to, a plugin or master
it can't find). Every error message says what to change.

## Limits

- **Scripts aren't read.** A record that only a script fetches by its ID (`Game.GetFormFromFile`, forms found at run
  time) looks unused. Script *properties* are read, so most scripted content is found. When a script is the only
  plausible way something is given, the record is usually Maybe rather than Orphaned.
- **Every scene and topic inside a running quest counts as used**, even ones whose conditions can never be met.
- **Interior cells are reached only through links.** An interior that only a script loads by its ID, or only the
  ship builder or an outpost uses, comes out Orphaned or Maybe.
- **Overrides are combined.** Links from every version of a record count, so a record a later plugin stops using
  still counts as used.
- It reads plugins with Mutagen.Bethesda.Starfield 0.54.4. A handful of official records can't be read (they show as
  CannotCheck or are left out of the walk). [MUTAGEN-GAPS.md](MUTAGEN-GAPS.md) lists them.
- Starfield only.

## Background and credit

sf-cut-check was inspired by georgearhitektive's
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

## License

GPL-3.0-only (see [LICENSE](LICENSE) and [NOTICE](NOTICE)). Privacy: [PRIVACY.md](PRIVACY.md), which says it sends
nothing anywhere.
