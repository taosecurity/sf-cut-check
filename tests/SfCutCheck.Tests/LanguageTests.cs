// --language (0.1.9, 2026-11-20): codes, finding string files, the exit-2 messages, and names read in
// another language, all on made-up files in a temp folder (no game files).
using BuildKit;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins.Binary.Parameters;
using Mutagen.Bethesda.Starfield;
using Mutagen.Bethesda.Strings;
using SfCutCheck;
using Xunit;

namespace SfCutCheck.Tests;

public sealed class LanguageTests : IDisposable
{
    readonly string dir = Directory.CreateTempSubdirectory("sfcutcheck-lang-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Theory]
    [InlineData("en", Language.English)]
    [InlineData("de", Language.German)]
    [InlineData("German", Language.German)]
    [InlineData("PTBR", Language.Portuguese_Brazil)]
    [InlineData("zhhans", Language.ChineseSimplified)]
    [InlineData("ja", Language.Japanese)]
    public void CodesAndNamesParse(string text, Language expected)
    {
        Assert.True(Languages.TryParse(text, out var lang));
        Assert.Equal(expected, lang);
    }

    [Theory]
    [InlineData("xx")]
    [InlineData("")]
    [InlineData("Czech")]   // in Mutagen's enum, but no file code: Mutagen can't read it
    public void UnknownCodesDont(string text) => Assert.False(Languages.TryParse(text, out _));

    void Loose(params string[] names)
    {
        Directory.CreateDirectory(Path.Combine(dir, "Strings"));
        foreach (var n in names) File.WriteAllBytes(Path.Combine(dir, "Strings", n), []);
    }

    [Fact]
    public void FoundListsOnlyThePluginsOwnLanguages()
    {
        Loose("Mod_en.strings", "Mod_de.STRINGS", "Mod_de.dlstrings", "Mod_xx.strings", "Other_fr.strings", "Mod_fr.ilstrings");
        Assert.Equal(["de", "en"], Languages.Found([dir], "Mod.esm"));
        Assert.Empty(Languages.Found([dir], "None.esm"));
    }

    [Fact]
    public void CheckGivesTheFix()
    {
        Loose("Mod_en.strings", "Mod_de.strings");
        Assert.Null(Languages.Check(null, [dir], "Mod.esm", out var none));
        Assert.Equal(Language.English, none);
        Assert.Null(Languages.Check("de", [dir], "Mod.esm", out var de));
        Assert.Equal(Language.German, de);

        var unknown = Languages.Check("xx", [dir], "Mod.esm", out _);
        Assert.NotNull(unknown);
        Assert.Contains("unknown language \"xx\"", unknown);
        Assert.Contains("de, en", unknown);

        var missing = Languages.Check("fr", [dir], "Mod.esm", out _);
        Assert.NotNull(missing);
        Assert.Contains("Mod_fr.STRINGS", missing);
        Assert.Contains("Languages found: de, en", missing);

        var nothing = Languages.Check("de", [dir], "Empty.esm", out _);
        Assert.Contains("No string files were found", nothing);
    }

    /// <summary>A localized made-up plugin with an English and a German name: Mutagen writes its Strings folder.</summary>
    (string Plugin, Mutagen.Bethesda.Plugins.FormKey Rifle, Mutagen.Bethesda.Plugins.FormKey Pistol) Localized()
    {
        const string name = "SctLang.esm";
        var mod = Plugin.New(name, PluginStyle.Full, "test", "made-up localized plugin for sfcutcheck tests");
        mod.UsingLocalization = true;
        var rifle = mod.Weapons.AddNew("SCT_LangRifle");
        rifle.Name = new TranslatedString(Language.English, "Rifle");
        rifle.Name.Set(Language.German, "Gewehr");
        var pistol = mod.Weapons.AddNew("SCT_LangPistol");
        pistol.Name = new TranslatedString(Language.English, "Pistol");   // no German text
        mod.WriteToBinary(Path.Combine(dir, name), new BinaryWriteParameters());
        return (name, rifle.FormKey, pistol.FormKey);
    }

    [Fact]
    public void NamesComeInTheChosenLanguage()
    {
        var (plugin, rifle, pistol) = Localized();
        Assert.Contains("de", Languages.Found([dir], plugin));
        Assert.Contains("en", Languages.Found([dir], plugin));

        var en = RefIndex.Build(dir, [plugin], new HashSet<string>(), _ => { });
        Assert.True(en.TryGetId(rifle, out var r1));
        Assert.Equal("Rifle", en.NameOf(r1));
        Assert.Empty(en.EnglishNameFallbacks);

        var de = RefIndex.Build(dir, [plugin], new HashSet<string>(), _ => { }, language: Language.German);
        Assert.True(de.TryGetId(rifle, out var r2));
        Assert.True(de.TryGetId(pistol, out var p2));
        Assert.Equal("Gewehr", de.NameOf(r2));
        Assert.Equal(Language.German, de.Language);
        // The pistol has no German text: it keeps its English name, and the plugin is counted.
        Assert.Equal("Pistol", de.NameOf(p2));
        Assert.Equal(1, de.EnglishNameFallbacks.GetValueOrDefault(plugin));
    }

    [Fact]
    public void ReportJsonNamesTheLanguage()
    {
        var run = new ReportRun("sfcutcheck", "0.0.0", "2026-11-20 00:00", dir, [], [], 0, 0, new(), "content",
            [], Language: "de");
        Assert.Contains("\"language\": \"de\"", Report.JsonText(run, []));
    }
}
