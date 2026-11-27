// The Starfield-specific rules: where the game starts using content (roots), which links point "up" to an owner
// (and so also count in the other direction), what counts as developer content, and which record types are
// structure rather than content (left out of the report unless --all-types).
using System.Text.RegularExpressions;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;

namespace SfCutCheck;

/// <summary>How sure a root is. A record reached only from a weaker tier gets a weaker verdict.</summary>
public enum Tier : byte { Strong = 0, Weak = 1, Dev = 2, None = 255 }

public sealed record RootRule(string Name, Tier Tier, string Means);

public static class Rules
{
    public static readonly RootRule StartGameQuest = new("start-game-quest", Tier.Strong, "a quest that starts when the game starts");
    public static readonly RootRule StoryManager = new("story-manager", Tier.Strong, "a story manager event node (the game runs these on events)");
    // 0.1.9 (2026-11-20): interior cells aren't roots by themselves any more. One is reached only when something
    // leads to it: a placed reference inside it that's reached (a door whose teleport target is in the cell, a quest
    // alias, a location's persistent references), the cell's own location, or a direct link (script property, pack-in,
    // story manager...). See RefIndex.AddWorldRoots.
    public static readonly RootRule World = new("world", Tier.Strong, "a worldspace that isn't developer content");
    public static readonly RootRule DefaultObjects = new("default-objects", Tier.Strong, "a default object (the engine looks these up by name)");
    public static readonly RootRule Galaxy = new("galaxy", Tier.Strong, "a star, planet or planet content tree");
    public static readonly RootRule Player = new("player", Tier.Strong, "the player character or the player reference");
    public static readonly RootRule GameplayOptions = new("gameplay-options", Tier.Strong, "a Gameplay Options group (the game's Gameplay Options menu lists every group)");
    public static readonly RootRule GameSettings = new("game-setting", Tier.Strong, "a game setting (the engine reads these by name)");
    // 0.2.0 (user's decision, 2026-11-20): a ship builder recipe whose only conditions are one ship technician vendor
    // keyword (VendorSM_*) on the subject, with or without a player-level gate, is offered by every technician whose
    // record carries that keyword. The walk makes it a strong root once such a technician is reached (Walk). The data
    // never links a technician to the ship builder (the game opens it from ship services dialogue), so this rule
    // supplies that link. Proof: NOTES "0.2.0 relay", step 2.
    public static readonly RootRule ShipTechnician = new("ship-technician", Tier.Strong, "a ship builder recipe a reached ship technician offers (its only conditions are a VendorSM_ keyword the technician carries, and maybe the player's level)");
    public const string ShipBuilderWorkbench = "WorkbenchShipBuildingKeyword";
    public const string TechnicianKeywordPrefix = "VendorSM_";
    public static readonly RootRule EnginePicked = new("engine-picked", Tier.Weak, "a record the engine picks by its conditions or keywords: the top of an idle animation or camera tree, a load screen, a sound or WWise keyword mapping, a movement type, a challenge, an affinity event, a photo mode feature");
    public static readonly RootRule Recipe = new("recipe", Tier.Weak, "a crafting, building or modding recipe (available only if its workbench and conditions allow)");
    public static readonly RootRule Research = new("research", Tier.Weak, "a research project (available only if its conditions allow)");
    public static readonly RootRule CheckedOnly = new("checked-only", Tier.Weak, "a running record checks it in a condition, but nothing starts, places or gives it (it may come from script code, which isn't seen)");
    // Used only by `scan --plugin` with the saved official scan (OfficialCache): a Bethesda record the mod refers to or
    // changes starts from the tier the full scan gave it.
    public static readonly RootRule OfficialStrong = new("official", Tier.Strong, "a Bethesda record the saved scan of the official files found reached");
    public static readonly RootRule OfficialWeak = new("official-maybe", Tier.Weak, "a Bethesda record the saved scan of the official files found only as Maybe");
    public static readonly RootRule OfficialDev = new("official-dev", Tier.Dev, "a Bethesda record the saved scan of the official files found only from test cells");
    public static readonly RootRule DevWorld = new("dev-world", Tier.Dev, "a worldspace or cell whose EditorID marks it as test, debug or cut content");

    public static readonly RootRule[] All =
        [StartGameQuest, StoryManager, World, DefaultObjects, Galaxy, Player, GameplayOptions, GameSettings, ShipTechnician, EnginePicked, Recipe, Research, CheckedOnly, DevWorld];

    /// <summary>The root rule a record meets by itself (worldspaces and cells are decided after the index is built).</summary>
    public static RootRule? RootOf(IMajorRecordGetter r) => r switch
    {
        IQuestGetter q when q.Data is { } d && d.Flags.HasFlag(Quest.Flag.StartGameEnabled) => StartGameQuest,
        IStoryManagerEventNodeGetter => StoryManager,
        IDefaultObjectGetter or IDefaultObjectManagerGetter => DefaultObjects,
        IPlanetGetter or IStarGetter or IPlanetContentManagerTreeGetter => Galaxy,
        IConstructibleObjectGetter => Recipe,
        IResearchProjectGetter => Research,
        // Perks are not roots any more (step 3, 2026-11-11): with every perk a root, CUT_Skill_Mindfulness came out
        // Maybe. Skills, traits and backgrounds are reached through their lists instead (checked in step 3).
        IGameSettingGetter => GameSettings,
        // Without this rule every mod's own options group came out Unreferenced (found trying --plugin on real mods).
        IGameplayOptionsGroupGetter => GameplayOptions,
        IIdleAnimationGetter i when i.ParentAnimation.IsNull => EnginePicked,
        ICameraPathGetter c when c.Parent.IsNull => EnginePicked,
        ILoadScreenGetter or ISoundKeywordMappingGetter or IWWiseKeywordMappingGetter or IMovementTypeGetter
            or IChallengeGetter or IAffinityEventGetter or IPhotoModeFeatureGetter => EnginePicked,
        _ => null,
    };

    /// <summary>Records the engine itself always uses.</summary>
    public static readonly FormKey[] PlayerForms =
    [
        FormKey.Factory("000007:Starfield.esm"),   // Player (NPC)
        FormKey.Factory("000014:Starfield.esm"),   // PlayerRef
    ];

    /// <summary>Links that name the record's owner. The owner reaches the record, so the index also adds the link
    /// in the other direction (owner -> record).</summary>
    public static IEnumerable<FormKey> OwnersOf(IMajorRecordGetter r)
    {
        switch (r)
        {
            case IAStoryManagerNodeGetter n: yield return n.Parent.FormKey; break;
            case IPlanetContentManagerBranchNodeGetter b: yield return b.ParentNode.FormKey; break;
            case IPlanetContentManagerContentNodeGetter c: yield return c.ParentNode.FormKey; break;
            case ISceneGetter s: yield return s.Quest.FormKey; break;
            case IDialogBranchGetter b: yield return b.Quest.FormKey; break;
            case IDialogTopicGetter t: yield return t.Quest.FormKey; break;
            case IMessageGetter m: yield return m.OwnerQuest.FormKey; break;
            case IPackageGetter p: yield return p.OwnerQuest.FormKey; break;
            case ISceneCollectionGetter sc: yield return sc.Quest.FormKey; break;
            case ISpeechChallengeGetter sp: yield return sp.Quest.FormKey; break;
            case ILocationGetter l: yield return l.ParentLocation.FormKey; break;
            case IIdleAnimationGetter i: yield return i.ParentAnimation.FormKey; break;
            case ICameraPathGetter cp: yield return cp.Parent.FormKey; break;
            // 0.1.9 (2026-11-20): a form list with AddToList joins the list it names (the DLCs add ship
            // modules, help pages, outpost furniture and Starborn values to base-game lists this way), so the target
            // list reaches it. Before, the 45 SFBGS006 hab lists came out Orphaned.
            case IFormListGetter fl: yield return fl.AddToList.FormKey; break;
        }
    }

    /// <summary>EditorID words that mark test, debug or cut content. Used for dev worlds and as hint labels.</summary>
    /// An EditorID is split into words at underscores, digits and CamelCase ("QATestCell_02" -> qa, test, cell), and
    /// a marker must be a whole word, so "Contest", "DevilsTower" and "OldMars" don't count.
    static readonly HashSet<string> DevWords = new(StringComparer.Ordinal)
    {
        "zz", "zzz", "xx", "xxx", "qa", "test", "tests", "debug", "dev", "cut", "unused", "deprecated", "obsolete",
        "delete", "deleted", "wip", "temp", "todo",
    };

    static readonly string[] DevPhrases = ["donotuse", "dontuse", "notused", "duplicate", "dupe"];

    static readonly Regex WordSplit = new(@"[^A-Za-z]+|(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", RegexOptions.CultureInvariant);

    public static IEnumerable<string> Words(string editorId) =>
        WordSplit.Split(editorId).Where(w => w.Length > 0).Select(w => w.ToLowerInvariant());

    // find --word splits like Words, but keeps digit runs as words of their own ("Neon01" -> neon, 01), so a
    // query with digits only matches those digits (0.2.1 code review round 1).
    static readonly Regex FindWordSplit = new(
        @"[^A-Za-z0-9]+|(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])|(?<=[A-Za-z])(?=[0-9])|(?<=[0-9])(?=[A-Za-z])",
        RegexOptions.CultureInvariant);

    static IEnumerable<string> FindWords(string s) =>
        FindWordSplit.Split(s).Where(w => w.Length > 0).Select(w => w.ToLowerInvariant());

    /// <summary>The search key of a find --word text: its words joined in lower case ("SamCoe" and "samcoe" both give
    /// "samcoe"), or null when the text has no letters or digits. Made once per search text.</summary>
    public static string? WordKey(string query)
    {
        var key = string.Concat(FindWords(query));
        return key.Length == 0 ? null : key;
    }

    /// <summary>True when a run of consecutive whole words in the EditorID, joined, equals the key, so "SamCoe",
    /// "samcoe" and "Sam_Coe" all match COM_SamCoe_Neon01 and COM_SAMCOE_Neon01, and "sam" matches too, but "Neon02"
    /// doesn't match Neon01. 0.2.1, C5.</summary>
    public static bool HasWordKey(string editorId, string key)
    {
        var e = FindWords(editorId).ToList();
        for (var i = 0; i < e.Count; i++)
        {
            var len = 0;
            for (var j = i; j < e.Count && len < key.Length; j++)
            {
                if (string.CompareOrdinal(key, len, e[j], 0, e[j].Length) != 0) break;
                len += e[j].Length;
                if (len == key.Length) return true;
            }
        }
        return false;
    }

    public static bool HasWords(string editorId, string query) => WordKey(query) is { } k && HasWordKey(editorId, k);

    /// <summary>The first dev marker word in an EditorID, or null. Also the hint label.</summary>
    public static string? Hint(string? editorId)
    {
        if (string.IsNullOrEmpty(editorId)) return null;
        // "zzTestCell" splits as "zz", "test", "cell"; "zzzOld" as "zzz", "old".
        if (Words(editorId).FirstOrDefault(DevWords.Contains) is { } w) return w;
        // Phrases that split into ordinary words: "xDoNotUse_EncShip_TridentLines".
        var flat = editorId.Replace("_", "").ToLowerInvariant();
        return DevPhrases.FirstOrDefault(flat.Contains);
    }

    /// <summary>Dev worlds use whole dev words only. The phrases ("DoNotUse", "DUPLICATE000") stay hint labels: the
    /// Creation Kit names copies "...DUPLICATE000", and "xDoNotUse...Template" ship blueprints are used by leveled
    /// ships (step 3, 2026-11-11).</summary>
    public static bool IsDevName(string? editorId) =>
        !string.IsNullOrEmpty(editorId) && Words(editorId).Any(DevWords.Contains);

    /// <summary>Record types a condition check doesn't reach: content the game must start, place or give for the
    /// player to meet it. A check on anything else (a condition record, global, form list, keyword, faction, actor
    /// value...) still reaches it, because the game reads that record to evaluate the check (step 3, 2026-11-11:
    /// without this, 686 condition records and 248 globals came out Orphaned).</summary>
    public static readonly HashSet<string> CheckDoesntReach = new(StringComparer.Ordinal)
    {
        "Quest", "Scene", "DialogTopic", "DialogResponses", "DialogBranch", "Npc", "LeveledNpc", "Weapon", "Armor",
        "Book", "MiscItem", "Ammunition", "Ingestible", "Key", "Note", "Terminal", "Activator", "Container", "Furniture",
        "Door", "Flora", "Static", "MoveableStatic", "Spell", "Perk", "MagicEffect", "ObjectModification",
        "WeaponModification", "ArmorModification", "NpcModification", "FloraModification", "PackIn", "GenericBaseForm",
        "LeveledItem", "Cell", "Worldspace", "Message", "Outfit", "Light", "IdleMarker", "Projectile", "Explosion",
        "Hazard",
    };

    /// <summary>Record types that are structure rather than content: left out of the report unless --all-types.</summary>
    public static readonly HashSet<string> Structural = new(StringComparer.Ordinal)
    {
        "PlacedObject", "PlacedNpc", "PlacedTrap", "NavigationMesh", "NavigationMeshInfoMap", "Layer", "ReferenceGroup",
        "PERS", "Cell", "Worldspace", "Keyword", "LocationReferenceType", "ActorValueModulation", "LayeredMaterialSwap",
        "SurfaceBlock", "SurfacePattern", "SurfacePatternConfig", "SurfacePatternStyle", "SurfaceTree", "FormFolderKeywordList",
        "MaterialPath", "Transform", "Curve3D", "CurveTable", "SnapTemplate", "SnapTemplateNode", "SnapTemplateBehavior",
        "NavigationMeshObstacleCoverManager", "BendableSpline", "Traversal", "TimeOfDayRecord", "ObjectVisibilityManager",
    };
}
