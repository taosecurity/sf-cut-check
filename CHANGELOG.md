# Changelog

## 0.1.9 (2026-11-20)

New name and accuracy release. The tool is now **sf-cut-check** (`sf-cut-check.exe`, repo
`github.com/taosecurity/sf-cut-check`).

Rename:

- The command is `sf-cut-check`. For this release only, the zip also holds `sfcutcheck.cmd`, which runs
  `sf-cut-check.exe` with the same arguments and prints one line on stderr saying the name changed.
- The saved scan of the official files moved to `%LOCALAPPDATA%\sf-cut-check` (the old `%LOCALAPPDATA%\sfcutcheck`
  folder can be deleted). Its environment variable is `SF_CUT_CHECK_CACHE_DIR`; the old `SFCUTCHECK_CACHE_DIR` still
  works. The first run after the update scans the official files again, because the rules changed.
- report.json's `tool` field and the report page's byline say `sf-cut-check`.

Accuracy (the results change; on the official Steam files, November 2026: Reached 6,861,715, Maybe 131,576,
DevOnly 126,675, Orphaned 488,432, Unreferenced 51,209, and 25,020 reported records, against 7,478,997, 7,357,
116,915, 5,167, 51,171 and 23,857 in 0.1.8):

- **Interior cells are no longer starting points by themselves.** One counts as used only when something leads to
  it: a door whose destination is inside it, a quest alias or location that names a reference inside it, its own
  location, a pack-in, a script property, or (for a ship's own cells) the placed ship they belong to. About 3,000 of
  the game's 16,707 interiors are no longer reached (cells are listed with `--all-types`), most of them pack-in
  storage cells. Interior test cells stay DevOnly.
  A cell the engine or a quest picks only by keyword, with no link in the data, shows as not used.
- For a cell, links from the records it holds itself (its navmeshes) no longer count, so a cell nothing outside
  leads to is Unreferenced.
- **Form lists with AddToList** (the DLCs use them to add ship modules, photo mode poses, help pages and outpost
  furniture to base-game lists) now count as used when the list they add to does. About 58,900 records, mostly
  ship habitat pack-ins from SFBGS006.esm, move from Orphaned to Maybe.
- A record reached through a record that is only checked in a condition now reads "via checked-only: <checked
  record>, checked by <checker>". Before, 28 rows on the official files named the wrong checker.
- Ship builder recipes stay Maybe: almost all of them depend on the ship technician's vendor keyword, the player's
  level or a quest, which the tool can't confirm from the files.

New and changed output:

- **`--language <code>`** picks the language of the in-game names in the reports, `why` and `find` (`en` by
  default; `de`, `es`, `fr`, `it`, `ja`, `pl`, `ptbr`, `zhhans`, or Mutagen's names such as `German`). An unknown
  code, or one with no string files, stops with exit code 2 and lists the languages found. report.json has a
  `language` field, and the report page's "What was checked" names the language. Verdicts never change with it.
- `why` prints a `name:` line (the in-game name) and a `reason:` line (the same text as the reports' reason). For a
  cell it lists the outside links into the references inside it first, under "links into the N record(s) inside it
  from outside". The step from a placed reference to its cell shows as the new link kind `in`, and the step from a
  list to a form list that adds to it as `owns`.
- `find` shows each record's in-game name in quotes after its EditorID, and the reason at the end of Maybe and
  DevOnly rows.
- `why` counts "linked from" the same way as the reports and the verdict: for a cell, the records inside it are
  counted separately ("plus N inside it").
- RESULTS.md lists the known engine-picked cells that show as not used.

Fixes from the release review:

- A cell whose location field Mutagen can't read no longer stops the whole scan; it's logged and skipped.
- `--language`: names in a plugin that isn't localized aren't counted as "kept in English"; string files are found
  without regard to case (Linux, Proton); the Localization archive is listed first, so the check no longer opens
  every archive.
- When the old `%LOCALAPPDATA%\sfcutcheck` folder is still there, a scan that saves its result says it can be
  deleted.

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
