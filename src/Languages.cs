// --language (0.1.9, 2026-11-20): which language the in-game names in the reports, `why` and `find` come
// from. The codes are the ones in the game's string file names (Starfield_de.STRINGS) and Mutagen's ISO codes for its
// Language enum (StringsUtility.GetIsoLanguageString); Mutagen's enum names (German) work too.
// A localized plugin keeps its strings in Data\Strings\<plugin>_<code>.STRINGS (.DLSTRINGS, .ILSTRINGS), or inside one
// of its archives "<plugin> - *.ba2" as strings/<plugin>_<code>.strings (Starfield.esm: "Starfield - Localization.ba2").
using Mutagen.Bethesda;
using Mutagen.Bethesda.Archives;
using Mutagen.Bethesda.Strings;

namespace SfCutCheck;

public static class Languages
{
    /// <summary>Every Mutagen language with a file code, in enum order (English first).</summary>
    public static IReadOnlyList<(Language Lang, string Code)> All { get; } = Enum.GetValues<Language>()
        .Select(l => { try { return (l, StringsUtility.GetIsoLanguageString(l)); } catch (NotImplementedException) { return (l, (string?)null); } })
        .Where(x => x.Item2 != null).Select(x => (x.l, x.Item2!)).ToList();

    public static string CodeOf(Language lang) => All.First(x => x.Lang == lang).Code;

    /// <summary>A file code (de, ptbr, zhhans) or an enum name (German), any case.</summary>
    public static bool TryParse(string text, out Language lang)
    {
        foreach (var (l, code) in All)
            if (string.Equals(text, code, StringComparison.OrdinalIgnoreCase) || string.Equals(text, l.ToString(), StringComparison.OrdinalIgnoreCase))
            { lang = l; return true; }
        lang = Language.English;
        return false;
    }

    /// <summary>The language codes a plugin has string files for, loose or in its archives, in any of the folders.</summary>
    public static SortedSet<string> Found(IEnumerable<string> dirs, string pluginFileName)
    {
        var found = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var stem = Path.GetFileNameWithoutExtension(pluginFileName);
        var prefix = stem + "_";
        void Add(string fileName)
        {
            // <plugin>_<code>.strings; the .dlstrings and .ilstrings files come with it.
            if (!fileName.EndsWith(".strings", StringComparison.OrdinalIgnoreCase) || !fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return;
            var code = fileName.Substring(prefix.Length, fileName.Length - prefix.Length - ".strings".Length);
            if (All.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase))) found.Add(code.ToLowerInvariant());
        }
        foreach (var dir in dirs.Where(Directory.Exists))
        {
            var loose = Path.Combine(dir, "Strings");
            if (Directory.Exists(loose))
                foreach (var f in Directory.EnumerateFiles(loose, prefix + "*.strings", new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive })) Add(Path.GetFileName(f));
            // Localization archives first; the others (meshes, textures, sounds) are listed only if those hold no
            // string files (0.1.9 review: listing every archive took seconds).
            var archives = Directory.EnumerateFiles(dir, stem + " - *.ba2", new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive })
                .OrderBy(a => Path.GetFileName(a).Contains("Localization", StringComparison.OrdinalIgnoreCase) ? 0 : 1).ToList();
            var fromLocalization = false;
            foreach (var ba2 in archives)
            {
                var isLocalization = Path.GetFileName(ba2).Contains("Localization", StringComparison.OrdinalIgnoreCase);
                if (fromLocalization && !isLocalization) break;
                var before = found.Count;
                try
                {
                    foreach (var f in Archive.CreateReader(GameRelease.Starfield, ba2).Files)
                    {
                        var p = f.Path.Replace('\\', '/');
                        if (p.StartsWith("strings/", StringComparison.OrdinalIgnoreCase) && p.IndexOf('/', "strings/".Length) < 0) Add(p.Substring("strings/".Length));
                    }
                }
                catch (Exception e) { Log.Write($"language check: can't list {ba2}: {e.GetType().Name}: {e.Message}"); }
                // Only string files from this folder's Localization archives end the search (loose files don't).
                if (isLocalization && found.Count > before) fromLocalization = true;
            }
        }
        Log.Write($"language check: string files for {pluginFileName}: {(found.Count == 0 ? "none" : string.Join(", ", found))}");
        return found;
    }

    /// <summary>The message for exit 2 when --language can't be used, or null when it can. Checked against
    /// Starfield.esm (every localized official file has the same languages).</summary>
    public static string? Check(string? requested, IEnumerable<string> dirs, string basePlugin, out Language lang)
    {
        lang = Language.English;
        if (requested == null) return null;
        var dirList = dirs.ToList();
        var have = Found(dirList, basePlugin);
        if (!TryParse(requested, out lang))
            return $"unknown language \"{requested}\" for --language. Use one of the codes in the game's string file names" +
                   (have.Count > 0 ? $"; {basePlugin} has: {string.Join(", ", have)}." : $": {string.Join(", ", All.Select(x => x.Code))}.");
        if (have.Contains(CodeOf(lang))) return null;
        return $"no {lang} string files ({Path.GetFileNameWithoutExtension(basePlugin)}_{CodeOf(lang)}.STRINGS) for {basePlugin} in {string.Join(" or ", dirList.Select(Path.GetFullPath))} " +
               $"(loose in Strings, or in \"{Path.GetFileNameWithoutExtension(basePlugin)} - *.ba2\"). " +
               (have.Count > 0 ? $"Languages found: {string.Join(", ", have)}. Pick one of those, or install the language (Steam: the game's Properties, Language)." :
                                 "No string files were found at all; check that --data is the game's Data folder.");
    }
}
