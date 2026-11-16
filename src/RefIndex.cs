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

namespace SfCutCheck;

/// <summary>Ref: a normal link. Contains: a record stored inside another. Owns: the reverse of an owner field.
/// Checks: the link appears only in the record's conditions; the walk doesn't follow it.</summary>
public enum LinkKind : byte { Ref = 0, Contains = 1, Owns = 2, Checks = 3 }

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
        else LinksByType[type] = LinksByType.GetValueOrDefault(type) + 1;
    }

    public static RefIndex Build(string dataDir, IReadOnlyList<string> plugins, ISet<string> skipTypes, Action<string> progress,
        IEnumerable<string>? extraDirs = null)
    {
        var ix = new RefIndex();
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
            long n = 0, before = ix.EdgeCount;
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
                    try { if (named.Name is { Length: > 0 } nm) ix.Names[self] = nm; }
                    catch (Exception e) { ix.NameFailures++; if (ix.NameFailures <= 5) Log.Write($"name of {r.FormKey} ({type}) unreadable: {e.GetType().Name}: {e.Message}"); }
                }
                if (Rules.RootOf(r) is { } rule) ix.Roots.Add((self, rule));
                foreach (var owner in Rules.OwnersOf(r))
                    if (!owner.IsNull) ix.Add(ix.Id(owner), self, LinkKind.Owns, type);

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

    /// <summary>Worldspaces, and interior cells (cells no worldspace holds), are roots: "world" normally, "dev-world"
    /// when their EditorID marks them as test, debug or cut content.</summary>
    void AddWorldRoots()
    {
        var inWorld = new HashSet<int>();
        for (var i = 0; i < From.Count; i++)
            if (Kind[i] == LinkKind.Contains && TypeOf(From[i]) == "Worldspace") inWorld.Add(To[i]);
        foreach (var id in Defined)
        {
            var t = TypeOf(id);
            if (t == "Worldspace" || (t == "Cell" && !inWorld.Contains(id)))
                Roots.Add((id, Rules.IsDevName(EditorIdOf(id)) ? Rules.DevWorld : Rules.World));
        }
    }

    public IEnumerable<(string Type, int Count)> CountByType() =>
        typeOf.Where(t => t != 255).GroupBy(t => t).Select(g => (typeNames[g.Key], g.Count())).OrderByDescending(x => x.Item2);
}
