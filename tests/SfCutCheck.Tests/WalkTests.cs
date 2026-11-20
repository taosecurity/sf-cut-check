// A made-up plugin (no game files) with one record for each verdict, built in a temp folder:
//   SCT_StartQuest   start-game quest; a script property points at SCT_WeaponByScript       -> both Reached
//                    its conditions test SCT_WeaponChecked (only) and SCT_WeaponByScript    -> Maybe (checked-only) / still Reached
//                    and compare SCT_GlobalInCheck (logic data, read by the check)          -> Reached
//   SCT_Scene        a scene stored inside SCT_StartQuest ("contains")                       -> Reached
//   SCT_Message      a message that names SCT_StartQuest as its owner ("owns")               -> Reached
//   SCT_List         a form list nothing uses, holding SCT_WeaponOrphan                       -> Unreferenced / Orphaned
//   SCT_WeaponAlone  a weapon nothing links to                                                -> Unreferenced
//   SCT_Recipe       a recipe that makes SCT_WeaponRecipe                                     -> both Maybe
//   SCT_Cell         an interior cell with a placed SCT_WeaponPlaced                          -> Reached
//   zzTestCell       an interior cell with a placed SCT_WeaponDev                             -> DevOnly
using BuildKit;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using SfCutCheck;
using Xunit;

namespace SfCutCheck.Tests;

public sealed class WalkTests : IDisposable
{
    const string PluginName = "SctTest.esm";
    readonly string dir = Directory.CreateTempSubdirectory("sfcutcheck-test-").FullName;
    readonly Walk walk;
    readonly Dictionary<string, FormKey> keys = new();

    public WalkTests()
    {
        var mod = Plugin.New(PluginName, PluginStyle.Full, "test", "made-up plugin for sfcutcheck tests");
        Weapon W(string edid) { var w = mod.Weapons.AddNew(edid); keys[edid] = w.FormKey; return w; }

        var byScript = W("SCT_WeaponByScript");
        var propAndChildCheck = W("SCT_WeaponPropChildCheck");
        var quest = mod.Quests.AddNew("SCT_StartQuest");
        quest.Data = new QuestData { Flags = Quest.Flag.StartGameEnabled };
        quest.VirtualMachineAdapter = new QuestAdapter
        {
            Scripts = new ExtendedList<ScriptEntry>
            {
                new ScriptEntry
                {
                    Name = "SCT_QuestScript",
                    Properties = new ExtendedList<ScriptProperty>
                    {
                        new ScriptObjectProperty { Name = "Weapon", Object = byScript.ToLink<IStarfieldMajorRecordGetter>() },
                        new ScriptObjectProperty { Name = "Other", Object = propAndChildCheck.ToLink<IStarfieldMajorRecordGetter>() },
                    },
                },
            },
        };
        keys["SCT_StartQuest"] = quest.FormKey;
        // Conditions: SCT_WeaponChecked is only tested; SCT_WeaponByScript is tested and also used.
        var checkedOnly = W("SCT_WeaponChecked");
        foreach (var target in new[] { checkedOnly, byScript })
        {
            var data = new GetIsIDConditionData();
            data.FirstParameter.Link.SetTo(target.FormKey);
            quest.DialogConditions.Add(new ConditionFloat { Data = data });
        }
        // A leveled list only checked, holding a weapon nothing else uses: both Maybe, and the weapon's reason names the
        // checked list and the quest that checks it (relay step 2, 2026-11-20).
        var inList = W("SCT_WeaponInCheckedList");
        var checkedList = mod.LeveledItems.AddNew("SCT_ListChecked");
        checkedList.Entries = new ExtendedList<LeveledItemEntry> { new LeveledItemEntry { Reference = inList.ToLink<IItemGetter>(), Count = 1, Level = 1 } };
        keys["SCT_ListChecked"] = checkedList.FormKey;
        var listCheck = new GetIsIDConditionData();
        listCheck.FirstParameter.Link.SetTo(checkedList.FormKey);
        quest.DialogConditions.Add(new ConditionFloat { Data = listCheck });
        // A global compared in a condition is read by the game, so a check reaches it.
        var global = mod.Globals.AddNew("SCT_GlobalInCheck");
        keys["SCT_GlobalInCheck"] = global.FormKey;
        quest.DialogConditions.Add(new ConditionGlobal { ComparisonValue = global.ToLink<IGlobalGetter>(), Data = new GetIsIDConditionData() });

        // A response inside the quest only checks SCT_WeaponPropChildCheck; the quest's own script property still uses it.
        var childCheck = new GetIsIDConditionData();
        childCheck.FirstParameter.Link.SetTo(propAndChildCheck.FormKey);
        var topic = new DialogTopic(mod) { EditorID = "SCT_Topic" };
        topic.Quest.SetTo(quest);
        topic.Responses.Add(new DialogResponses(mod) { Conditions = new ExtendedList<Condition> { new ConditionFloat { Data = childCheck } } });
        quest.DialogTopics.Add(topic);

        var scene = new Scene(mod) { EditorID = "SCT_Scene" };
        scene.Quest.SetTo(quest);
        quest.Scenes.Add(scene);
        keys["SCT_Scene"] = scene.FormKey;

        var message = mod.Messages.AddNew("SCT_Message");
        message.OwnerQuest.SetTo(quest);
        keys["SCT_Message"] = message.FormKey;

        var orphan = W("SCT_WeaponOrphan");
        var list = mod.FormLists.AddNew("SCT_List");
        list.Items.Add(orphan.ToLink<IStarfieldMajorRecordGetter>());
        keys["SCT_List"] = list.FormKey;

        W("SCT_WeaponAlone");
        // The mod's own Gameplay Options group with its debug switch, as BuildKit makes them for our mods.
        var debug = BuildKit.GameplayOptions.Debug(mod, "SCT_DebugOption", "test lines");
        keys["SCT_DebugOption"] = debug.FormKey;
        keys["SCT_Options"] = BuildKit.GameplayOptions.Group(mod, "SCT_Options", "SCT Test", [debug]).FormKey;

        var made = W("SCT_WeaponRecipe");
        var recipe = mod.ConstructibleObjects.AddNew("SCT_Recipe");
        recipe.CreatedObject.SetTo(made.FormKey);
        keys["SCT_Recipe"] = recipe.FormKey;

        // Relay step 4 (2026-11-20): a list whose AddToList names another list joins that list, so whatever reaches
        // the target reaches it (the DLC's SFBGS006AddToList_SMSSet_* ship module lists work this way).
        var recipeList = mod.FormLists.AddNew("SCT_RecipeList");
        var recipe2 = mod.ConstructibleObjects.AddNew("SCT_RecipeOfList");
        recipe2.CreatedObject.SetTo(recipeList.FormKey);
        var addToRecipe = mod.FormLists.AddNew("SCT_AddToRecipeList");
        addToRecipe.AddToList.SetTo(recipeList.FormKey);
        addToRecipe.Items.Add(W("SCT_WeaponAddedToRecipeList").ToLink<IStarfieldMajorRecordGetter>());
        var scriptList = mod.FormLists.AddNew("SCT_ScriptList");
        var addToScript = mod.FormLists.AddNew("SCT_AddToScriptList");
        addToScript.AddToList.SetTo(scriptList.FormKey);
        addToScript.Items.Add(W("SCT_WeaponAddedToScriptList").ToLink<IStarfieldMajorRecordGetter>());
        foreach (var l in new[] { recipeList, addToRecipe, scriptList, addToScript }) keys[l.EditorID!] = l.FormKey;
        keys["SCT_RecipeOfList"] = recipe2.FormKey;

        // Interior cells (relay step 3a, 2026-11-20): none is a root by itself except the dev cell.
        var placed = W("SCT_WeaponPlaced");
        var dev = W("SCT_WeaponDev");
        var cell = new Cell(mod) { EditorID = "SCT_Cell" };
        cell.Temporary.Add(new PlacedObject(mod) { Base = new FormLink<IPlaceableObjectGetter>(placed.FormKey) });
        var devCell = new Cell(mod) { EditorID = "zzTestCell" };
        devCell.Temporary.Add(new PlacedObject(mod) { Base = new FormLink<IPlaceableObjectGetter>(dev.FormKey) });
        // SCT_Cell is reached by the quest's script property; its door leads to a door in SCT_CellBehindDoor.
        var doorBase = mod.Doors.AddNew("SCT_Door");
        var behind = new Cell(mod) { EditorID = "SCT_CellBehindDoor" };
        var doorB = new PlacedObject(mod) { Base = new FormLink<IPlaceableObjectGetter>(doorBase.FormKey) };
        behind.Persistent.Add(doorB);
        behind.Temporary.Add(new PlacedObject(mod) { Base = new FormLink<IPlaceableObjectGetter>(W("SCT_WeaponBehindDoor").FormKey) });
        var doorA = new PlacedObject(mod) { Base = new FormLink<IPlaceableObjectGetter>(doorBase.FormKey) };
        doorA.TeleportDestination = new TeleportDestination { Door = new FormLink<IPlacedObjectGetter>(doorB.FormKey) };
        cell.Persistent.Add(doorA);
        // A cell nothing leads to: the cell is Unreferenced (its own references don't count), what's in it Orphaned.
        var lost = new Cell(mod) { EditorID = "SCT_CellLost" };
        lost.Temporary.Add(new PlacedObject(mod) { Base = new FormLink<IPlaceableObjectGetter>(W("SCT_WeaponInLostCell").FormKey) });
        // A cell whose location the quest names: the location leads into it.
        var location = mod.Locations.AddNew("SCT_Location");
        var byLocation = new Cell(mod) { EditorID = "SCT_CellByLocation" };
        byLocation.Location.SetTo(location.FormKey);
        // Step 3b: a ship's instance cell names its placed ship (a linked reference); the ship is placed in SCT_Cell.
        var shipBase = mod.Activators.AddNew("SCT_ShipBase");
        var placedShip = new PlacedObject(mod) { Base = new FormLink<IPlaceableObjectGetter>(shipBase.FormKey) };
        cell.Persistent.Add(placedShip);
        var shipCell = new Cell(mod) { EditorID = "SCT_CellShipInstance" };
        shipCell.LinkedReferences.Add(new LinkedReferences
        {
            KeywordOrReference = new FormLink<IKeywordLinkedReferenceGetter>(mod.Keywords.AddNew("SCT_SpaceshipLinkedInterior").FormKey),
            Reference = new FormLink<IPlacedGetter>(placedShip.FormKey),
        });
        shipCell.Temporary.Add(new PlacedObject(mod) { Base = new FormLink<IPlaceableObjectGetter>(W("SCT_WeaponInShip").FormKey) });
        // Step 3b: a cell whose only inbound link comes from a record inside it (like a navmesh naming its cell):
        // Unreferenced, not Orphaned.
        var selfOnly = new Cell(mod) { EditorID = "SCT_CellSelfLinked" };
        var selfRef = new PlacedObject(mod) { Base = new FormLink<IPlaceableObjectGetter>(W("SCT_WeaponInSelfLinked").FormKey) };
        selfRef.LinkedReferences.Add(new LinkedReferences { Reference = new FormLink<IPlacedGetter>(selfOnly.FormKey) });
        selfOnly.Temporary.Add(selfRef);
        foreach (var c in new[] { cell, devCell, behind, lost, byLocation, shipCell, selfOnly }) keys[c.EditorID!] = c.FormKey;
        keys["SCT_Location"] = location.FormKey;
        quest.VirtualMachineAdapter.Scripts[0].Properties.Add(new ScriptObjectProperty { Name = "Cell", Object = new FormLink<IStarfieldMajorRecordGetter>(cell.FormKey) });
        quest.VirtualMachineAdapter.Scripts[0].Properties.Add(new ScriptObjectProperty { Name = "Location", Object = location.ToLink<IStarfieldMajorRecordGetter>() });
        quest.VirtualMachineAdapter.Scripts[0].Properties.Add(new ScriptObjectProperty { Name = "List", Object = scriptList.ToLink<IStarfieldMajorRecordGetter>() });
        mod.Cells.Add(new CellBlock { SubBlocks = new ExtendedList<CellSubBlock> { new CellSubBlock { Cells = new ExtendedList<Cell> { cell, devCell, behind, lost, byLocation, shipCell, selfOnly } } } });

        using (var g = new GameData(dir)) PluginWriter.Write(mod, dir, g, new WriteOptions { GapCheck = false, FixEffects = false });
        var index = RefIndex.Build(dir, [PluginName], new HashSet<string>(), _ => { });
        walk = new Walk(index);
    }

    Verdict V(string edid)
    {
        Assert.True(walk.Index.TryGetId(keys[edid], out var id), $"{edid} isn't in the index");
        return walk.VerdictOf(id);
    }

    [Theory]
    [InlineData("SCT_StartQuest", Verdict.Reached)]
    [InlineData("SCT_WeaponByScript", Verdict.Reached)]
    [InlineData("SCT_WeaponChecked", Verdict.Maybe)]
    [InlineData("SCT_WeaponPropChildCheck", Verdict.Reached)]
    [InlineData("SCT_GlobalInCheck", Verdict.Reached)]
    [InlineData("SCT_Scene", Verdict.Reached)]
    [InlineData("SCT_Message", Verdict.Reached)]
    [InlineData("SCT_WeaponPlaced", Verdict.Reached)]
    [InlineData("SCT_List", Verdict.Unreferenced)]
    [InlineData("SCT_WeaponOrphan", Verdict.Orphaned)]
    [InlineData("SCT_WeaponAlone", Verdict.Unreferenced)]
    [InlineData("SCT_Options", Verdict.Reached)]
    [InlineData("SCT_DebugOption", Verdict.Reached)]
    [InlineData("SCT_Recipe", Verdict.Maybe)]
    [InlineData("SCT_WeaponRecipe", Verdict.Maybe)]
    [InlineData("SCT_WeaponDev", Verdict.DevOnly)]
    [InlineData("SCT_Cell", Verdict.Reached)]
    [InlineData("SCT_CellBehindDoor", Verdict.Reached)]
    [InlineData("SCT_WeaponBehindDoor", Verdict.Reached)]
    [InlineData("SCT_CellByLocation", Verdict.Reached)]
    [InlineData("SCT_CellLost", Verdict.Unreferenced)]
    [InlineData("SCT_WeaponInLostCell", Verdict.Orphaned)]
    [InlineData("SCT_CellShipInstance", Verdict.Reached)]
    [InlineData("SCT_WeaponInShip", Verdict.Reached)]
    [InlineData("SCT_CellSelfLinked", Verdict.Unreferenced)]
    [InlineData("SCT_WeaponInSelfLinked", Verdict.Orphaned)]
    [InlineData("zzTestCell", Verdict.DevOnly)]
    [InlineData("SCT_RecipeList", Verdict.Maybe)]
    [InlineData("SCT_AddToRecipeList", Verdict.Maybe)]
    [InlineData("SCT_WeaponAddedToRecipeList", Verdict.Maybe)]
    [InlineData("SCT_ScriptList", Verdict.Reached)]
    [InlineData("SCT_AddToScriptList", Verdict.Reached)]
    [InlineData("SCT_WeaponAddedToScriptList", Verdict.Reached)]
    public void Verdicts(string edid, Verdict expected) => Assert.Equal(expected, V(edid));

    [Fact]
    public void CheckedOnlyNamesItsOwnRule()
    {
        walk.Index.TryGetId(keys["SCT_WeaponChecked"], out var id);
        Assert.Same(Rules.CheckedOnly, walk.ReasonOf(id));
        Assert.Equal(keys["SCT_StartQuest"], walk.Index.KeyOf(walk.Pred[id]));
    }

    int Id(string edid) { Assert.True(walk.Index.TryGetId(keys[edid], out var id), $"{edid} isn't in the index"); return id; }
    string D(string edid) => walk.Index.Describe(Id(edid));

    [Theory]
    [InlineData("SCT_ListChecked", Verdict.Maybe)]
    [InlineData("SCT_WeaponInCheckedList", Verdict.Maybe)]
    public void CheckedListAndItsItemAreMaybe(string edid, Verdict expected) => Assert.Equal(expected, V(edid));

    [Fact]
    public void CheckedOnlyReasonNamesTheChecker()
    {
        Assert.Equal($"checked-only: checked by {D("SCT_StartQuest")}", walk.ReasonText(Id("SCT_WeaponChecked")));
        Assert.Equal($"checked-only: checked by {D("SCT_StartQuest")}", walk.ReasonText(Id("SCT_ListChecked")));
    }

    [Fact]
    public void RecordReachedThroughACheckedOneNamesBoth()
    {
        // Before 2026-11-20 this said "checked by" the leveled list, which only holds the weapon.
        var id = Id("SCT_WeaponInCheckedList");
        Assert.Same(Rules.CheckedOnly, walk.ReasonOf(id));
        Assert.Equal((Id("SCT_ListChecked"), Id("SCT_StartQuest")), walk.CheckedBy(id));
        Assert.Equal($"via checked-only: {D("SCT_ListChecked")}, checked by {D("SCT_StartQuest")}", walk.ReasonText(id));
    }

    [Fact]
    public void OtherReasonsKeepTheirWording()
    {
        Assert.Equal($"via recipe: {D("SCT_Recipe")}", walk.ReasonText(Id("SCT_WeaponRecipe")));
        Assert.Equal("nothing links to it", walk.ReasonText(Id("SCT_WeaponAlone")));
        Assert.Equal("only records the game never reaches link to it", walk.ReasonText(Id("SCT_WeaponOrphan")));
        Assert.Null(walk.CheckedBy(Id("SCT_WeaponRecipe")));
    }

    [Fact]
    public void ChecksLinkKindIsKeptBothWays()
    {
        var q = Id("SCT_StartQuest");
        var c = Id("SCT_WeaponChecked");
        Assert.Equal(LinkKind.Checks, walk.KindBetween(q, c));
        Assert.Contains((q, LinkKind.Checks), walk.Inbound(c));
        Assert.Contains((c, LinkKind.Checks), walk.Outbound(q));
        // A record that's both checked and used keeps a normal link, not a checks link.
        Assert.Equal(LinkKind.Ref, walk.KindBetween(q, Id("SCT_WeaponByScript")));
    }

    [Fact]
    public void CheckedOnlyReasonReachesEveryReport()
    {
        var id = Id("SCT_WeaponInCheckedList");
        var reason = walk.ReasonText(id);
        var row = new ReportRow("Maybe", "Weapon", walk.Index.KeyOf(id).ToString(), PluginName, "SCT_WeaponInCheckedList",
            null, null, reason, 1, [D("SCT_ListChecked")]);
        var run = new ReportRun("sfcutcheck", "test", "2026-11-20 00:00", dir, [PluginName], [], 1, 1,
            new() { ["Maybe"] = 1 }, "every record type", [(Rules.CheckedOnly.Name, "Weak", 0, Rules.CheckedOnly.Means)]);
        Assert.Contains(reason, Report.Csv([row]));
        using var doc = System.Text.Json.JsonDocument.Parse(Report.JsonText(run, [row]));
        Assert.Equal(reason, doc.RootElement.GetProperty("records")[0].GetProperty("reason").GetString());
        Assert.Contains(System.Text.Json.JsonSerializer.Serialize(reason).Trim('"'), Report.Html(run, [row]));
    }

    [Fact]
    public void PathNamesTheRootAndEveryStep()
    {
        walk.Index.TryGetId(keys["SCT_WeaponByScript"], out var id);
        var path = walk.PathTo(id);
        Assert.Equal(2, path.Count);
        Assert.Equal(keys["SCT_StartQuest"], walk.Index.KeyOf(path[0]));
        Assert.Same(Rules.StartGameQuest, walk.RootOf[path[0]]);
    }

    [Fact]
    public void ContainerLinksArentCountedTwice()
    {
        // The cell holds the placed weapon; the weapon must be linked from the placed reference, not from the cell.
        walk.Index.TryGetId(keys["SCT_WeaponPlaced"], out var id);
        var inbound = walk.Inbound(id).ToList();
        Assert.Single(inbound);
        Assert.Equal("PlacedObject", walk.Index.TypeOf(inbound[0].From));
    }

    [Fact]
    public void InteriorCellsAreNoRootsByThemselves()
    {
        Assert.DoesNotContain(walk.Index.Roots, r => r.Rule == Rules.World && walk.Index.TypeOf(r.Id) == "Cell");
        Assert.Contains((Id("zzTestCell"), Rules.DevWorld), walk.Index.Roots);
        Assert.Equal(7, walk.Index.InteriorCells);
        Assert.Equal(1, walk.Index.InteriorDevRoots);
        Assert.Equal(1, walk.Index.InteriorLocationLinks);
        Assert.Equal(1, walk.Index.InteriorLinkedRefLinks);
    }

    [Fact]
    public void LinksFromInsideACellDontCountForIt()
    {
        // The reference inside SCT_CellSelfLinked does link to the cell, but only from inside.
        var c = Id("SCT_CellSelfLinked");
        Assert.Contains(walk.Inbound(c), x => x.Kind == LinkKind.Ref && walk.Index.TypeOf(x.From) == "PlacedObject");
        Assert.Empty(walk.InboundFromOutside(c));
        // The ship's instance cell: the placed ship leads in ("owns"), and the path shows it.
        var path = walk.PathTo(Id("SCT_CellShipInstance"));
        Assert.Equal("PlacedObject", walk.Index.TypeOf(path[^2]));
        Assert.Equal(LinkKind.Owns, walk.KindBetween(path[^2], path[^1]));
    }

    [Fact]
    public void AddToListJoinsItsTargetList()
    {
        // quest -> SCT_ScriptList -owns-> SCT_AddToScriptList -> weapon
        var path = walk.PathTo(Id("SCT_WeaponAddedToScriptList"));
        Assert.Equal(Id("SCT_ScriptList"), path[^3]);
        Assert.Equal(LinkKind.Owns, walk.KindBetween(path[^3], path[^2]));
        Assert.Equal($"via recipe: {D("SCT_RecipeOfList")}", walk.ReasonText(Id("SCT_WeaponAddedToRecipeList")));
    }

    [Fact]
    public void DoorPathGoesThroughTheTargetDoorIntoItsCell()
    {
        // quest -> SCT_Cell -> door A -> door B -> SCT_CellBehindDoor
        var path = walk.PathTo(Id("SCT_CellBehindDoor"));
        Assert.Equal(5, path.Count);
        Assert.Equal(Id("SCT_StartQuest"), path[0]);
        Assert.Equal(Id("SCT_Cell"), path[1]);
        Assert.Equal("PlacedObject", walk.Index.TypeOf(path[3]));
        Assert.Equal(LinkKind.In, walk.KindBetween(path[3], path[4]));
        Assert.Equal("nothing links to it", walk.ReasonText(Id("SCT_CellLost")));
    }

    [Theory]
    [InlineData("zzTestCell", true)]
    [InlineData("QA_Smoke", true)]
    [InlineData("DebugRoom", true)]
    [InlineData("CUT_Dialogue", true)]
    [InlineData("NewAtlantis", false)]
    [InlineData("Contest01", false)]
    [InlineData("DevilsTower", false)]
    [InlineData("OldMars", false)]
    [InlineData("CutsceneIntro", false)]
    [InlineData("QATestCell02", true)]
    [InlineData("MQ101_DevRoom", true)]
    [InlineData("PackInStorageCellDUPLICATE000", false)]
    [InlineData("BlueprintExteriorxDoNotUseLvlShipTridentLines", false)]
    public void DevNames(string edid, bool dev) => Assert.Equal(dev, Rules.IsDevName(edid));

    [Theory]
    [InlineData("xDoNotUse_EncShip_TridentLines_M01", "donotuse")]
    [InlineData("LL_Weapon_CutlassDUPLICATE000", "duplicate")]
    [InlineData("CUT_AmmoMag", "cut")]
    [InlineData("NewAtlantis", null)]
    public void HintLabels(string edid, string? hint) => Assert.Equal(hint, Rules.Hint(edid));

    public void Dispose()
    {
        try { Directory.Delete(dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
