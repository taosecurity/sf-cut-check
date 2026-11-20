// The reference index: every record (FormKey) gets a small integer id; every link from one record to another is
// kept as a pair of ids plus a kind. Records themselves are not kept, so memory grows with the link count only.
//
// Two things Mutagen does that the index undoes (found in the step 1 spike, 2026-11-11):
//  - A record that holds other records (a worldspace its cells, a cell its placed references and navmeshes, a quest
//    its dialogue topics, a topic its responses) reports all of their links as its own. The index keeps a container's
//    own links only (its links minus those of the records inside it) and adds one "contains" link to each record
//    directly inside it. A container link that a record inside it also has is dropped; reachability doesn't change
//    (the inner record is reached through "contains"), only which record is named as evidence.
//  - Surface trees repeat the same pattern many times. Each record's links are de-duplicated.
// Links that name an owner (Rules.OwnersOf) are also added in the other direction, as "owns".
// Overrides: links from every version of a record are kept (union).
using BuildKit;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Aspects;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
using Mutagen.Bethesda.Strings;

namespace SfCutCheck;

/// <summary>Ref: a normal link. Contains: a record stored inside another. Owns: the reverse of an owner field.
/// Checks: the link appears only in the record's conditions; the walk doesn't follow it.
/// In: from a placed reference to the interior cell it's in (relay step 3a, 2026-11-20): a reached reference (the target
/// of a door, a quest alias) leads into its cell. Not counted as "something links to it" for Orphaned.</summary>
public enum LinkKind : byte { Ref = 0, Contains = 1, Owns = 2, Checks = 3, In = 4 }

public sealed class RefIndex
{
    readonly Dictionary<FormKey, int> ids = new();
    readonly List<FormKey> keys = new();
    readonly List<byte> typeOf = new();
    readonly List<string> typeNames = new();
    readonly Dictionary<string, byte> typeIds = new();
    public readonly List<int> From = new();
    public readonly List<int> To = new();
    public readonly List<LinkKind> Kind = new();
    public readonly List<(FormKey Key, string Type, string Error)> Failed = new();
    public readonly HashSet<int> FailedIds = new();
    public readonly Dictionary<string, long> LinksByType = new();
    public readonly Dictionary<int, string> EditorIds = new();
    public readonly Dictionary<int, string> Names = new();
    public int NameFailures;
    /// <summary>--language: the language names were read in, and the plugins whose names (some or all) stayed English
    /// because they have no strings in it.</summary>
    public Language Language { get; private set; } = Language.English;
    public readonly Dictionary<string, long> EnglishNameFallbacks = new(StringComparer.OrdinalIgnoreCase);
    public string? NameOf(int id) => Names.GetValueOrDefault(id);
    public readonly HashSet<int> Defined = new();
    /// <summary>Records that are roots by themselves (worldspaces and interior cells are added by AddWorldRoots).</summary>
    public readonly List<(int Id, RootRule Rule)> Roots = new();
    public readonly List<string> PluginsRead = new();
    public readonly List<string> Skipped = new();

    public int NodeCount => keys.Count;
    public int EdgeCount => From.Count;
    public long ContainsCount { get; private set; }
    public long OwnsCount { get; private set; }
    public long InCount { get; private set; }
    /// <summary>Interior cells by kind, worked out by AddWorldRoots (logged; shown by the scan).</summary>
    public int InteriorCells, InteriorDevRoots, InteriorLocationLinks;
    // A cell's location (Cell.Location), from every version of the cell: the location leads into the cell.
    readonly List<(int Cell, int Location)> cellLocations = new();
    // A cell's linked references (Cell.LinkedReferences): a ship's instance cell names its placed ship this way.
    readonly List<(int Cell, int Reference)> cellLinkedRefs = new();
    public int InteriorLinkedRefLinks;
    /// <summary>Form lists whose AddToList names another list (relay step 4): each version counts once.</summary>
    public int AddToListLinks;
    public long VersionsRead { get; private set; }
    public long DanglingCount { get; private set; }

    public FormKey KeyOf(int id) => keys[id];
    public string TypeOf(int id) => typeOf[id] == 255 ? "?" : typeNames[typeOf[id]];
    public string? EditorIdOf(int id) => EditorIds.GetValueOrDefault(id);
    public bool TryGetId(FormKey k, out int id) => ids.TryGetValue(k, out id);

    /// <summary>"Weapon 0012AB:Starfield.esm EditorID" for messages.</summary>
    public string Describe(int id) => $"{TypeOf(id)} {KeyOf(id)}{(EditorIdOf(id) is { } e ? " " + e : "")}";

    int Id(FormKey k)
    {
        if (ids.TryGetValue(k, out var id)) return id;
        id = keys.Count;
        ids[k] = id;
        keys.Add(k);
        typeOf.Add(255);
        return id;
    }

    byte TypeId(string name)
    {
        if (typeIds.TryGetValue(name, out var t)) return t;
        if (typeNames.Count >= 255) throw new InvalidOperationException("more than 254 record types");
        t = (byte)typeNames.Count;
        typeNames.Add(name);
        typeIds[name] = t;
        return t;
    }

    public static string TypeName(IMajorRecordGetter r) => r.GetType().Name.Replace("BinaryOverlay", "");

    static bool IsPlaced(string type) => type.StartsWith("Placed", StringComparison.Ordinal);

    public long CheckLinksFound { get; private set; }

    /// <summary>A record's links as ids, without repeats and without links to itself. Null if Mutagen failed. With
    /// checks != null, links that occur only inside the record's conditions go there instead.</summary>
    HashSet<int>? LinksOf(IMajorRecordGetter r, string type, int self, bool record, HashSet<int>? checks = null) =>
        LinksOf(r, type, self, record, checks, out _);

    HashSet<int>? LinksOf(IMajorRecordGetter r, string type, int self, bool record, HashSet<int>? checks, out bool conditionsFailed)
    {
        conditionsFailed = false;
        var total = new Dictionary<FormKey, int>();
        try
        {
            foreach (var l in r.EnumerateFormLinks())
                if (!l.FormKey.IsNull) total[l.FormKey] = total.GetValueOrDefault(l.FormKey) + 1;
        }
        catch (Exception e)
        {
            if (record && FailedIds.Add(self)) Failed.Add((r.FormKey, type, $"{e.GetType().Name}: {e.Message}"));
            return null;
        }
        Dictionary<FormKey, int>? inConditions = null;
        if (checks != null)
        {
            try { inConditions = Conditions.LinkCounts(r); }
            catch (Exception e)
            {
                conditionsFailed = true;
                if (record) Failed.Add((r.FormKey, type, $"conditions: {e.GetType().Name}: {e.Message}"));
            }
        }
        var set = new HashSet<int>();
        foreach (var (k, n) in total)
        {
            var id = Id(k);
            if (id == self) continue;
            // Every occurrence is inside a condition: the record only tests it.
            if (inConditions != null && inConditions.GetValueOrDefault(k) >= n) { checks!.Add(id); if (record) CheckLinksFound++; }
            else set.Add(id);
        }
        return set;
    }

    // Link sets worked out for a record while its container was read, kept until the scan reaches the record itself.
    // Mutagen lists a container before the records inside it, so an entry is normally used within the same container.
    // Capped (CacheLimit links in all) so a huge worldspace can't hold millions of sets at once; past the cap a record
    // is simply read again, as before 0.1.8.
    const long CacheLimit = 8_000_000;
    readonly Dictionary<FormKey, (HashSet<int> Links, HashSet<int> Checks)> cache = new();
    long cachedLinks;
    public long CacheHits, CacheMisses, CacheFull;

    void Cache(FormKey key, HashSet<int> links, HashSet<int> checks)
    {
        long size = links.Count + checks.Count;
        if (cachedLinks + size > CacheLimit) { CacheFull++; return; }
        if (cache.TryAdd(key, (links, checks))) cachedLinks += size;
    }

    bool TakeCached(FormKey key, out (HashSet<int> Links, HashSet<int> Checks) value)
    {
        if (cache.Remove(key, out value)) { cachedLinks -= value.Links.Count + value.Checks.Count; CacheHits++; return true; }
        CacheMisses++;
        return false;
    }

    void Add(int from, int to, LinkKind kind, string type)
    {
        From.Add(from);
        To.Add(to);
        Kind.Add(kind);
        if (kind == LinkKind.Contains) ContainsCount++;
        else if (kind == LinkKind.Owns) OwnsCount++;
        else if (kind == LinkKind.In) InCount++;
        else LinksByType[type] = LinksByType.GetValueOrDefault(type) + 1;
    }

    public static RefIndex Build(string dataDir, IReadOnlyList<string> plugins, ISet<string> skipTypes, Action<string> progress,
        IEnumerable<string>? extraDirs = null, Language language = Language.English)
    {
        var ix = new RefIndex();
        ix.Language = language;
        using var game = new GameData(dataDir, extraDirs);
        foreach (var p in plugins)
        {
            if (!GameData.IsPlainPluginName(p))
            {
                ix.Skipped.Add(p);
                progress($"\"{GameData.Printable(p)}\": not a plain plugin file name, skipped");
                continue;
            }
            var path = game.Locate(p);
            Log.Write($"plugin {p}: {(path == null ? "not found in the Data folder" : Log.Describe(path))}");
            IStarfieldModGetter? mod;
            // BuildKit 0.3.1 refuses a plugin whose master list holds a path instead of a file name (security fix).
            try { mod = game.TryOpen(p); }
            catch (InvalidDataException e)
            {
                ix.Skipped.Add(p);
                progress($"{p}: its master list is damaged or unsafe, skipped");
                Log.Write($"skipped {p}: {e.Message}");
                continue;
            }
            if (mod == null)
            {
                ix.Skipped.Add(p);
                progress($"{p}: not found (or one of its masters is missing), skipped");
                Log.Write($"skipped {p}: {(path == null ? "the file isn't there" : "one of its masters isn't there: " + string.Join(", ", GameData.HeaderMasters(path).Where(m => game.Locate(m) == null)))}");
                continue;
            }
            ix.PluginsRead.Add(p);
            long n = 0, before = ix.EdgeCount, fallbacks = 0, translated = 0;
            foreach (var r in mod.EnumerateMajorRecords())
            {
                var type = TypeName(r);
                if (skipTypes.Contains(type)) continue;
                n++;
                var self = ix.Id(r.FormKey);
                ix.Defined.Add(self);
                ix.typeOf[self] = ix.TypeId(type);
                if (!IsPlaced(type) && r.EditorID is { Length: > 0 } edid) ix.EditorIds[self] = edid;
                // In-game names (from the string tables) for content records only, for the reports.
                if (!IsPlaced(type) && !Rules.Structural.Contains(type) && r is INamedGetter named)
                {
                    try
                    {
                        // --language (relay step 6): another language comes from the same string lookup Mutagen opened
                        // for the plugin; a plugin with no strings in that language (or a record with no translation)
                        // keeps its English name, counted per plugin and logged.
                        if (language == Language.English) { if (named.Name is { Length: > 0 } nm) ix.Names[self] = nm; }
                        else if (r is ITranslatedNamedGetter { Name: { } ts } && ts.TryLookup(language, out var tr) && tr is { Length: > 0 }) { ix.Names[self] = tr; translated++; }
                        else if (named.Name is { Length: > 0 } nmEn) { ix.Names[self] = nmEn; fallbacks++; }
                    }
                    catch (Exception e) { ix.NameFailures++; if (ix.NameFailures <= 5) Log.Write($"name of {r.FormKey} ({type}) unreadable: {e.GetType().Name}: {e.Message}"); }
                }
                if (Rules.RootOf(r) is { } rule) ix.Roots.Add((self, rule));
                if (r is ICellGetter cellRec)
                {
                    if (!cellRec.Location.IsNull) ix.cellLocations.Add((self, ix.Id(cellRec.Location.FormKey)));
                    // A ship's instance cells name the placed ship they belong to (linked references with the keywords
                    // SpaceshipLinkedInterior / SpaceshipLinkedExterior): the placed ship leads into them (step 3b).
                    try
                    {
                        if (cellRec.LinkedReferences is { } lrs)
                            foreach (var lr in lrs)
                                if (!lr.Reference.IsNull) ix.cellLinkedRefs.Add((self, ix.Id(lr.Reference.FormKey)));
                    }
                    catch (Exception e) { Log.Write($"linked references of cell {r.FormKey} unreadable: {e.GetType().Name}: {e.Message}"); }
                }
                foreach (var owner in Rules.OwnersOf(r))
                    if (!owner.IsNull) ix.Add(ix.Id(owner), self, LinkKind.Owns, type);
                if (r is IFormListGetter { AddToList.IsNull: false }) ix.AddToListLinks++;

                // If Mutagen fails on the record's links (often because of one navmesh inside a cell), keep going with
                // no links of its own, so the records inside it still get their "contains" links.
                HashSet<int> checks, own;
                if (ix.TakeCached(r.FormKey, out var cached))
                {
                    (own, checks) = cached;
                    ix.CheckLinksFound += checks.Count;
                }
                else
                {
                    checks = new HashSet<int>();
                    own = ix.LinksOf(r, type, self, record: true, checks) ?? new HashSet<int>();
                }

                if (r is IMajorRecordGetterEnumerable holder)
                {
                    // Everything stored inside this record, at any depth.
                    var inside = holder.EnumerateMajorRecords().Where(c => !ReferenceEquals(c, r) && c.FormKey != r.FormKey).ToList();
                    if (inside.Count > 0)
                    {
                        var insideIds = new HashSet<int>();
                        var deeper = new HashSet<int>();   // records inside a record that is itself inside this one
                        foreach (var c in inside)
                        {
                            var cid = ix.Id(c.FormKey);
                            insideIds.Add(cid);
                            own.Remove(cid);
                            if (c is IMajorRecordGetterEnumerable inner)
                                foreach (var g in inner.EnumerateMajorRecords())
                                    if (g.FormKey != c.FormKey) deeper.Add(ix.Id(g.FormKey));
                        }
                        // Drop from the container only what the inner records really use. A link an inner record
                        // merely checks stays the container's own (step 3 fix, 2026-11-11: DialogueFCNeon's script
                        // property for FFNeonZ08 was lost because its responses check FFNeonZ08).
                        // 0.1.8 (code review finding 10): only the records directly inside are read here, because
                        // Mutagen gives a record the links of everything inside it too. Before, every record at any
                        // depth was read again, so a placed reference was read about three times. What is read here
                        // is kept for when the scan reaches that record itself (ix.Cache).
                        foreach (var c in inside)
                        {
                            var cid = ix.Id(c.FormKey);
                            if (deeper.Contains(cid)) continue;
                            var childChecks = new HashSet<int>();
                            var cl = ix.LinksOf(c, TypeName(c), cid, record: false, childChecks, out bool condFailed);
                            if (cl != null)
                            {
                                own.ExceptWith(cl); checks.ExceptWith(cl); checks.ExceptWith(childChecks);
                                if (!condFailed && !skipTypes.Contains(TypeName(c))) ix.Cache(c.FormKey, cl, childChecks);
                            }
                            else if (c is IMajorRecordGetterEnumerable failed)
                            {
                                // Mutagen failed on this record's links (often one navmesh in a cell): fall back to
                                // the records inside it, one by one, as before.
                                foreach (var g in failed.EnumerateMajorRecords())
                                {
                                    if (g.FormKey == c.FormKey) continue;
                                    var gChecks = new HashSet<int>();
                                    var gl = ix.LinksOf(g, TypeName(g), ix.Id(g.FormKey), record: false, gChecks);
                                    if (gl != null) { own.ExceptWith(gl); checks.ExceptWith(gl); checks.ExceptWith(gChecks); }
                                }
                            }
                        }
                        foreach (var cid in insideIds)
                            if (!deeper.Contains(cid)) ix.Add(self, cid, LinkKind.Contains, type);
                    }
                }
                foreach (var to in own) ix.Add(self, to, LinkKind.Ref, type);
                foreach (var to in checks) if (!own.Contains(to)) ix.Add(self, to, LinkKind.Checks, type);
            }
            ix.VersionsRead += n;
            // Left over: records the scan never reached on their own (should be none); free them per plugin.
            if (ix.cache.Count > 0) { Log.Write($"link cache: {ix.cache.Count:N0} sets left unused after {p}"); ix.cache.Clear(); ix.cachedLinks = 0; }
            progress($"{p}: {n:N0} records, {ix.EdgeCount - before:N0} links");
            Log.Write($"read {p}: {n:N0} records, {ix.EdgeCount - before:N0} links; {ix.Failed.Count} records Mutagen failed on so far");
            if (language != Language.English)
            {
                Log.Write($"names in {language} from {p}: {translated:N0}; kept in English (no {language} string for them): {fallbacks:N0}");
                if (fallbacks > 0) ix.EnglishNameFallbacks[p] = fallbacks;
            }
        }
        foreach (var k in Rules.PlayerForms)
            if (ix.ids.TryGetValue(k, out var pid) && ix.Defined.Contains(pid)) ix.Roots.Add((pid, Rules.Player));
        ix.AddWorldRoots();
        // A record overridden by the DLC or a creation meets its root rule once per version: count it once (code
        // review finding 4, 2026-11-11). Overrides are still combined: a later version that drops the rule (for
        // example clears a quest's start-game flag) doesn't remove the root. README "Limits" says so.
        var distinct = ix.Roots.Distinct().ToList();
        ix.Roots.Clear();
        ix.Roots.AddRange(distinct);
        foreach (var f in ix.Failed) Log.Write($"Mutagen failed on {f.Type} {f.Key}: {f.Error} (its own links are left out; see MUTAGEN-GAPS.md)");
        foreach (var g in ix.Roots.GroupBy(r => r.Rule)) Log.Write($"root rule {g.Key.Name}: {g.Count():N0} roots");
        Log.Write($"names read: {ix.Names.Count:N0}; unreadable: {ix.NameFailures}");
        Log.Write($"link cache: {ix.CacheHits:N0} records read once instead of again, {ix.CacheMisses:N0} read on their own, " +
                  $"{ix.CacheFull:N0} not kept (cap {CacheLimit:N0} links)");
        ix.DanglingCount = ix.To.LongCount(t => !ix.Defined.Contains(t));
        return ix;
    }

    /// <summary>Worldspaces are roots: "world" normally, "dev-world" when their EditorID marks them as test, debug or cut
    /// content. Interior cells (cells no worldspace holds) whose EditorID marks them as developer content stay
    /// "dev-world" roots, as before. Every other interior cell is no root any more (relay step 3a, 2026-11-20): it's
    /// reached only through links. Two ways in are added here: each placed reference directly inside the cell links
    /// to it ("in"), so a reached reference (a door's teleport target, a quest alias, a location's persistent
    /// references) leads into its cell; and the cell's location links to it ("owns"). Direct links to the cell (a script
    /// property, a pack-in, the story manager) were already there.</summary>
    void AddWorldRoots()
    {
        var inWorld = new HashSet<int>();
        var inside = new List<(int Cell, int Child)>();
        for (var i = 0; i < From.Count; i++)
        {
            if (Kind[i] != LinkKind.Contains) continue;
            var ft = TypeOf(From[i]);
            if (ft == "Worldspace") inWorld.Add(To[i]);
            else if (ft == "Cell" && IsPlaced(TypeOf(To[i]))) inside.Add((From[i], To[i]));
        }
        var interior = new HashSet<int>();   // interior cells that aren't developer content
        foreach (var id in Defined)
        {
            var t = TypeOf(id);
            if (t == "Worldspace")
                Roots.Add((id, Rules.IsDevName(EditorIdOf(id)) ? Rules.DevWorld : Rules.World));
            else if (t == "Cell" && !inWorld.Contains(id))
            {
                InteriorCells++;
                if (Rules.IsDevName(EditorIdOf(id))) { Roots.Add((id, Rules.DevWorld)); InteriorDevRoots++; }
                else interior.Add(id);
            }
        }
        // A pair may repeat when the DLC or a creation overrides the cell; the walk drops repeated links.
        foreach (var (cell, child) in inside)
            if (interior.Contains(cell)) Add(child, cell, LinkKind.In, "PlacedReference");
        foreach (var (cell, loc) in cellLocations.Distinct())
            if (interior.Contains(cell)) { Add(loc, cell, LinkKind.Owns, "Location"); InteriorLocationLinks++; }
        // Step 3b (2026-11-20): a ship's instance cell (BlueprintShips "REFR<ship>ID<ref>INST..." cells) is built for
        // one placed ship and names it as a linked reference; the cell itself has no other way in.
        foreach (var (cell, reference) in cellLinkedRefs.Distinct())
            if (interior.Contains(cell)) { Add(reference, cell, LinkKind.Owns, "PlacedReference"); InteriorLinkedRefLinks++; }
        Log.Write($"interior cells: {InteriorCells:N0}; dev-world roots {InteriorDevRoots:N0}; the other {interior.Count:N0} are no roots " +
                  $"and are reached only through links: {InCount:N0} \"in\" links from the placed references inside them, " +
                  $"{InteriorLocationLinks:N0} from their locations, {InteriorLinkedRefLinks:N0} from the placed ships their linked references name");
        Log.Write($"form lists that join another list (AddToList): {AddToListLinks:N0}; the target list leads to each (kind owns)");
    }

    public IEnumerable<(string Type, int Count)> CountByType() =>
        typeOf.Where(t => t != 255).GroupBy(t => t).Select(g => (typeNames[g.Key], g.Count())).OrderByDescending(x => x.Item2);
}
