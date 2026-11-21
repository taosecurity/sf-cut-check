// 0.2.0: the saved official scan keeps the records Mutagen couldn't read (RefIndex.FailedIds and CellFieldFailedIds),
// so a `scan --plugin` run from the saved scan marks them CannotCheck the same way a full read does.
//
// Two made-up plugins in a temp folder:
//   SctOfficial.esm  stands in for the official files: SCT_WeaponBroken (a weapon), SCT_CellBroken (an interior cell)
//                    and SCT_WeaponFine; nothing in it links to any of them.
//   SctMod.esm       a "mod" with SctOfficial.esm as its master: a quest whose script names all three, and a weapon of
//                    its own nothing links to.
// A clean plugin can't make Mutagen fail on a field, so the failures are put into the index by hand after the read,
// exactly where RefIndex.Build would put them (the weapon's links, the cell's fields).
using BuildKit;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Binary.Parameters;
using Mutagen.Bethesda.Starfield;
using Noggog;
using SfCutCheck;
using Xunit;

namespace SfCutCheck.Tests;

public sealed class CacheFailedTests : IDisposable
{
    const string OfficialName = "SctOfficial.esm";
    const string ModName = "SctMod.esm";
    readonly string dir = Directory.CreateTempSubdirectory("sfcutcheck-cache-").FullName;
    readonly string cacheDir;
    readonly FormKey broken, brokenCell, fine, modWeapon;

    public CacheFailedTests()
    {
        cacheDir = Path.Combine(dir, "cache");
        var off = Plugin.New(OfficialName, PluginStyle.Full, "test", "made-up stand-in for the official files");
        broken = off.Weapons.AddNew("SCT_WeaponBroken").FormKey;
        fine = off.Weapons.AddNew("SCT_WeaponFine").FormKey;
        var cell = new Cell(off) { EditorID = "SCT_CellBroken" };
        brokenCell = cell.FormKey;
        off.Cells.Add(new CellBlock { SubBlocks = new ExtendedList<CellSubBlock> { new CellSubBlock { Cells = new ExtendedList<Cell> { cell } } } });
        off.WriteToBinary(Path.Combine(dir, OfficialName), new BinaryWriteParameters());

        var mod = Plugin.New(ModName, PluginStyle.Full, "test", "made-up mod for the saved-scan tests");
        modWeapon = mod.Weapons.AddNew("SCT_ModWeapon").FormKey;
        var quest = mod.Quests.AddNew("SCT_ModQuest");
        quest.VirtualMachineAdapter = new QuestAdapter
        {
            Scripts = new ExtendedList<ScriptEntry>
            {
                new ScriptEntry
                {
                    Name = "SCT_ModQuestScript",
                    Properties = new ExtendedList<ScriptProperty>
                    {
                        new ScriptObjectProperty { Name = "Broken", Object = new FormLink<IStarfieldMajorRecordGetter>(broken) },
                        new ScriptObjectProperty { Name = "Cell", Object = new FormLink<IStarfieldMajorRecordGetter>(brokenCell) },
                        new ScriptObjectProperty { Name = "Fine", Object = new FormLink<IStarfieldMajorRecordGetter>(fine) },
                    },
                },
            },
        };
        using (var g = new GameData(dir))
        {
            g.Open(OfficialName);   // the master must be in the load order to write the mod's FormIDs
            PluginWriter.Write(mod, dir, g, new WriteOptions { GapCheck = false, FixEffects = false });
        }
    }

    /// <summary>Marks the two broken records the way RefIndex.Build does when Mutagen fails on them.</summary>
    void Break(RefIndex ix)
    {
        Assert.True(ix.TryGetId(broken, out var b));
        Assert.True(ix.TryGetId(brokenCell, out var c));
        ix.FailedIds.Add(b);
        ix.CellFieldFailedIds.Add(c);
    }

    static Verdict V(Walk w, FormKey k)
    {
        Assert.True(w.Index.TryGetId(k, out var id));
        return w.VerdictOf(id);
    }

    OfficialCache SaveAndLoad()
    {
        var full = RefIndex.Build(dir, [OfficialName], new HashSet<string>(), _ => { });
        Break(full);
        OfficialCache.FromWalk(new Walk(full), dir, [OfficialName]).Save(cacheDir, [OfficialName]);
        var loaded = OfficialCache.TryLoad(cacheDir, dir, [OfficialName]);
        Assert.NotNull(loaded);
        return loaded!;
    }

    [Fact]
    public void SavedScanKeepsUnreadableRecords()
    {
        var cache = SaveAndLoad();
        Assert.Equal(1, cache.FailedLinksCount);
        Assert.Equal(1, cache.FailedCellFieldsCount);
        var text = File.ReadAllText(Directory.GetFiles(cacheDir, "official-*.txt").Single());
        Assert.Contains($"{broken}\t!links", text);
        Assert.Contains($"{brokenCell}\t!cell-fields", text);
        Assert.Contains($"format {OfficialCache.FormatVersion}", text);
    }

    [Fact]
    public void PluginRunFromTheSavedScanMarksThemLikeAFullRead()
    {
        // A full read of the official stand-in and the mod together (what --no-cache does).
        var fresh = RefIndex.Build(dir, [OfficialName, ModName], new HashSet<string>(), _ => { });
        Break(fresh);
        var freshWalk = new Walk(fresh);

        // The mod alone, with the official records from the saved scan (what a cached --plugin run does).
        var cache = SaveAndLoad();
        var small = RefIndex.Build(dir, [ModName], new HashSet<string>(), _ => { });
        cache.SeedRoots(small);
        var cachedWalk = new Walk(small);

        Assert.Equal(Verdict.CannotCheck, V(freshWalk, broken));
        Assert.Equal(Verdict.CannotCheck, V(freshWalk, brokenCell));
        foreach (var k in new[] { broken, brokenCell, fine, modWeapon })
            Assert.Equal(V(freshWalk, k), V(cachedWalk, k));
        Assert.Contains("couldn't read its links", cachedWalk.ReasonText(small.TryGetId(broken, out var b) ? b : -1));
        Assert.Contains("cell's location or linked references", cachedWalk.ReasonText(small.TryGetId(brokenCell, out var c) ? c : -1));
    }

    [Fact]
    public void UnknownLineMeansScanAgain()
    {
        // The key holds FormatVersion, so an older file is never found; a file with a line it doesn't know is not guessed at.
        var key = OfficialCache.KeyFor(dir, [OfficialName]);
        Assert.Matches("^[0-9a-f]{16}$", key);
        var cache = SaveAndLoad();
        Assert.Equal(key, cache.Key);
        File.WriteAllText(Directory.GetFiles(cacheDir, "official-*.txt").Single(), $"# made x\n{broken}\t!unknown\n");
        Assert.Null(OfficialCache.TryLoad(cacheDir, dir, [OfficialName]));   // an unknown mark: rebuilt, not guessed
    }

    public void Dispose()
    {
        try { Directory.Delete(dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
