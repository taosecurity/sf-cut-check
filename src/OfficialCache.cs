// The saved result of a full scan of the official files, so `scan --plugin` doesn't have to read the whole game every
// time (user's test, 2026-11-11: "I asked to check one esm and it's doing the whole set again").
//
// What's saved: every official record the walk did NOT reach strongly, with its tier (Weak, Dev or None). Every other
// record an official plugin defines counts as Strong. That's about 700,000 lines instead of 7.6 million. Since 0.2.0
// (FormatVersion 2) it also lists the records Mutagen couldn't read, so they're CannotCheck there as in a full read.
//
// When it's used: the key is each official plugin's name, size and modified time, plus the tool's rules version. A
// Starfield patch, or adding or removing a Creation, changes the key, and the scan runs again by itself.
//
// Why the answers match a full read: an official record can't link to a mod's records, so a mod's record is reached
// only through the mod itself (its own roots, or its versions of official records, which start from the official
// record's saved tier) or through its non-official masters, which are read in full. Tests compare both ways.
using System.Security.Cryptography;
using System.Text;
using Mutagen.Bethesda.Plugins;

namespace SfCutCheck;

public sealed class OfficialCache
{
    /// <summary>Change this whenever Rules.cs or the walk changes what counts as reached, so old caches are rebuilt.</summary>
    public const string RulesVersion = "rules-2026-11-20d";

    /// <summary>The layout of the saved file. Change it whenever what's saved changes, so an older file isn't misread:
    /// it's part of the key, so an older file is simply not found and the scan runs again. Kept apart from RulesVersion
    /// (a format change doesn't change any verdict). 2 (0.2.0): adds the records Mutagen couldn't read.</summary>
    public const int FormatVersion = 2;

    // Marks for the lines of records Mutagen couldn't read (0.2.0): "!links" = its own links (RefIndex.FailedIds),
    // "!cell-fields" = a cell's location or linked references (RefIndex.CellFieldFailedIds). A record can have both.
    const string FailedLinksMark = "!links";
    const string FailedCellFieldsMark = "!cell-fields";

    public string Key { get; }
    public string MadeUtc { get; }
    readonly Dictionary<FormKey, Tier> notStrong;
    readonly HashSet<FormKey> failedLinks;
    readonly HashSet<FormKey> failedCellFields;
    public HashSet<string> Plugins { get; }

    OfficialCache(string key, string madeUtc, Dictionary<FormKey, Tier> notStrong, HashSet<FormKey> failedLinks,
        HashSet<FormKey> failedCellFields, IEnumerable<string> plugins)
    {
        Key = key;
        MadeUtc = madeUtc;
        this.notStrong = notStrong;
        this.failedLinks = failedLinks;
        this.failedCellFields = failedCellFields;
        Plugins = new HashSet<string>(plugins, StringComparer.OrdinalIgnoreCase);
    }

    public int NotStrongCount => notStrong.Count;
    public int FailedLinksCount => failedLinks.Count;
    public int FailedCellFieldsCount => failedCellFields.Count;

    /// <summary>The tier the full official scan gave this record (Strong unless the cache lists it).</summary>
    public Tier TierOf(FormKey k) => notStrong.TryGetValue(k, out var t) ? t : Tier.Strong;

    public bool IsOfficial(FormKey k) => Plugins.Contains(k.ModKey.FileName);

    // 0.1.9 rename: SF_CUT_CHECK_CACHE_DIR, with the old SFCUTCHECK_CACHE_DIR still read through 0.2.0 (CHANGELOG
    // says 0.2.0 is the last release that reads it).
    public static string DefaultDir =>
        Environment.GetEnvironmentVariable("SF_CUT_CHECK_CACHE_DIR") is { Length: > 0 } d ? d
        : Environment.GetEnvironmentVariable("SFCUTCHECK_CACHE_DIR") is { Length: > 0 } old ? old
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "sf-cut-check");

    /// <summary>A note when 0.1.8's saved-scan folder is still there (0.1.9 review): its file is stale after the rename
    /// and the rules change, so the user may delete it. Null when there is nothing to say.</summary>
    public static string? OldDirNote()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(appData)) return null;   // never a relative "sfcutcheck" in the current folder
        var old = Path.Combine(appData, "sfcutcheck");
        if (!Directory.Exists(old)) return null;
        // No note when the folder in use is the old one or inside it (deleting it would delete the new scan).
        try
        {
            var oldFull = Path.GetFullPath(old).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            var nowFull = Path.GetFullPath(DefaultDir).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            if (nowFull.StartsWith(oldFull, StringComparison.OrdinalIgnoreCase)) return null;
        }
        catch (Exception) { return null; }   // a cache path that can't be resolved: say nothing rather than stop
        return $"note: the saved scan of older versions in {old} isn't used any more; you can delete that folder.";
    }

    /// <summary>The key for the official plugins as they are on disk now.</summary>
    public static string KeyFor(string dataDir, IReadOnlyList<string> officialPlugins)
    {
        var sb = new StringBuilder(RulesVersion).Append('\n').Append("format ").Append(FormatVersion).Append('\n');
        foreach (var p in officialPlugins)
        {
            var fi = new FileInfo(Path.Combine(dataDir, p));
            sb.Append(p.ToLowerInvariant()).Append('|').Append(fi.Exists ? fi.Length : -1).Append('|')
              .Append(fi.Exists ? fi.LastWriteTimeUtc.Ticks : 0).Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())))[..16].ToLowerInvariant();
    }

    static string PathFor(string dir, string key) => Path.Combine(dir, $"official-{key}.txt");

    /// <summary>The saved scan for these plugins as they are now, or null (with the reason in the log).</summary>
    public static OfficialCache? TryLoad(string dir, string dataDir, IReadOnlyList<string> officialPlugins)
    {
        var key = KeyFor(dataDir, officialPlugins);
        var path = PathFor(dir, key);
        if (!File.Exists(path))
        {
            Log.Write($"no saved official scan for the current game files (looked for {path})");
            return null;
        }
        try
        {
            var notStrong = new Dictionary<FormKey, Tier>();
            var failedLinks = new HashSet<FormKey>();
            var failedCellFields = new HashSet<FormKey>();
            string made = "?";
            foreach (var line in File.ReadLines(path))
            {
                if (line.StartsWith("# made ")) { made = line[7..]; continue; }
                if (line.Length == 0 || line[0] == '#') continue;
                var tab = line.IndexOf('\t');
                if (tab < 0 || !FormKey.TryFactory(line.Substring(0, tab), out var fk)) throw new InvalidDataException($"bad line: {line}");
                switch (line.Substring(tab + 1))
                {
                    case FailedLinksMark: failedLinks.Add(fk); break;
                    case FailedCellFieldsMark: failedCellFields.Add(fk); break;
                    case "Weak": notStrong[fk] = Tier.Weak; break;
                    case "Dev": notStrong[fk] = Tier.Dev; break;
                    case "None": notStrong[fk] = Tier.None; break;
                    default: throw new InvalidDataException($"bad line: {line}");
                }
            }
            Log.Write($"loaded the saved official scan {Log.Describe(path)}: {notStrong.Count:N0} records not strongly reached, " +
                      $"{failedLinks.Count:N0} with links Mutagen couldn't read, {failedCellFields.Count:N0} cells with unreadable fields");
            return new OfficialCache(key, made, notStrong, failedLinks, failedCellFields, officialPlugins);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Log.Write($"the saved official scan {path} can't be read ({e.Message}); scanning again");
            return null;
        }
    }

    /// <summary>Makes the cache from a walk over exactly the official plugins.</summary>
    public static OfficialCache FromWalk(Walk w, string dataDir, IReadOnlyList<string> officialPlugins)
    {
        var ix = w.Index;
        var notStrong = new Dictionary<FormKey, Tier>();
        foreach (var id in ix.Defined)
            if (w.TierOf[id] != Tier.Strong) notStrong[ix.KeyOf(id)] = w.TierOf[id];
        var failedLinks = ix.FailedIds.Select(ix.KeyOf).ToHashSet();
        var failedCellFields = ix.CellFieldFailedIds.Select(ix.KeyOf).ToHashSet();
        return new OfficialCache(KeyFor(dataDir, officialPlugins), DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC", notStrong,
            failedLinks, failedCellFields, officialPlugins);
    }

    /// <summary>Writes the cache; a failure is logged, not fatal (the next run just scans again).</summary>
    public void Save(string dir, IReadOnlyList<string> officialPlugins)
    {
        var path = PathFor(dir, Key);
        try
        {
            Directory.CreateDirectory(dir);
            var tmp = path + ".tmp";
            using (var wtr = new StreamWriter(tmp, false, new UTF8Encoding(false)))
            {
                wtr.WriteLine("# sf-cut-check: saved scan of the official files. Safe to delete; it's rebuilt when needed.");
                wtr.WriteLine($"# made {MadeUtc}");
                wtr.WriteLine($"# {RulesVersion}; format {FormatVersion}; plugins: {string.Join(", ", officialPlugins)}");
                wtr.WriteLine("# records not strongly reached (every other official record is): FormKey<TAB>tier");
                foreach (var (k, t) in notStrong.OrderBy(kv => kv.Key.ToString(), StringComparer.Ordinal))
                    wtr.WriteLine($"{k}\t{(t == Tier.None ? "None" : t.ToString())}");
                wtr.WriteLine($"# records Mutagen couldn't read: FormKey<TAB>{FailedLinksMark} (its links) or {FailedCellFieldsMark} (a cell's location or linked references)");
                foreach (var k in failedLinks.Select(k => k.ToString()).OrderBy(s => s, StringComparer.Ordinal))
                    wtr.WriteLine($"{k}\t{FailedLinksMark}");
                foreach (var k in failedCellFields.Select(k => k.ToString()).OrderBy(s => s, StringComparer.Ordinal))
                    wtr.WriteLine($"{k}\t{FailedCellFieldsMark}");
            }
            File.Move(tmp, path, overwrite: true);
            Log.Write($"saved the official scan to {Log.Describe(path)}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Write($"couldn't save the official scan to {path}: {e.Message} (the next --plugin run scans the game again)");
        }
    }

    /// <summary>Adds the official records a small index refers to as roots at their saved tiers, and marks those Mutagen
    /// couldn't read in the full scan the same way a full read would (0.2.0), so their verdict is CannotCheck as there.</summary>
    public int SeedRoots(RefIndex ix)
    {
        var n = 0;
        var marked = 0;
        for (var id = 0; id < ix.NodeCount; id++)
        {
            var k = ix.KeyOf(id);
            if (!IsOfficial(k)) continue;
            if (failedLinks.Contains(k) && ix.FailedIds.Add(id)) marked++;
            if (failedCellFields.Contains(k) && ix.CellFieldFailedIds.Add(id)) marked++;
            var rule = TierOf(k) switch
            {
                Tier.Strong => Rules.OfficialStrong,
                Tier.Weak => Rules.OfficialWeak,
                Tier.Dev => Rules.OfficialDev,
                _ => null,
            };
            if (rule == null) continue;
            ix.Roots.Add((id, rule));
            n++;
        }
        if (marked > 0) Log.Write($"marked {marked:N0} official records the saved scan lists as unreadable by Mutagen");
        return n;
    }
}
