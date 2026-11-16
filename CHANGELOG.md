# Changelog

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
