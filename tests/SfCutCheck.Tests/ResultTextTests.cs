// 0.2.1 (Round 3): C2 the RESULT line names what it leaves out, C5 find --word with a multi-word query.
using SfCutCheck;
using Xunit;

public class ResultTextTests
{
    [Fact]
    public void LeftOutStructureRecordsAreNamed_NotReachesAll()
    {
        // 45 records, 37 of them unlinked structure records the default types leave out; the 8 content records are reached.
        var s = ResultText.Build("MyMod.esm", 45, 0, 37, 37, 0, 0, typesGiven: false);
        Assert.DoesNotContain("reaches all of them", s);
        Assert.Contains("all of its content records", s);
        Assert.Contains("leaves out 37 structure records that nothing in the game reaches", s);
        Assert.Contains("--all-types", s);
    }

    [Fact]
    public void NothingLeftOut_KeepsTheOldSentence()
    {
        var s = ResultText.Build("MyMod.esm", 3, 0, 0, 0, 1, 0, typesGiven: false);
        Assert.StartsWith("RESULT: MyMod.esm adds 3 records, and the game reaches all of them. 1 is reached only through", s);
        Assert.DoesNotContain("leaves out", s);
    }

    [Fact]
    public void DeadRecords_StillCountedAndLeftOutStillNamed()
    {
        var s = ResultText.Build("MyMod.esm", 90, 7, 70, 70, 0, 0, typesGiven: false);
        Assert.Contains("nothing in the game reaches 7 of them", s);
        Assert.Contains("leaves out 70 structure records", s);
    }

    [Fact]
    public void WithTypes_SaysTheTypesAreTheReason()
    {
        var s = ResultText.Build("MyMod.esm", 10, 0, 1, 1, 0, 0, typesGiven: true);
        Assert.Contains("records of the types you chose", s);
        Assert.Contains("leaves out 1 record that nothing in the game reaches, because its type isn't in --types", s);
    }

    // Code review round 2 (2026-11-27): the counts come from ForPlugin, which is tested here, not from Program.cs.
    static Dictionary<Verdict, int> Counts(params (Verdict V, int N)[] c) => c.ToDictionary(x => x.V, x => x.N);

    [Fact]
    public void ForPlugin_LeftOutDevOnlyStructure_NotReachesAll()
    {
        // 13 records: 8 content records Reached, 5 structure records reached only from test cells (left out by default).
        var r = ResultText.ForPlugin("MyMod.esm", Counts((Verdict.Reached, 8), (Verdict.DevOnly, 5)), [], typesGiven: false);
        Assert.Equal((0, 0, 5), (r.Dead, r.LeftOutDead, r.LeftOut));
        Assert.DoesNotContain("reaches all of them", r.Text);
        Assert.Contains("all of its content records", r.Text);
    }

    [Fact]
    public void ForPlugin_CountsReportedAndLeftOutDead()
    {
        // 20 records: 10 Reached; reported 2 Orphaned, 1 Unreferenced, 1 Maybe; left out 4 Orphaned, 2 DevOnly.
        var counts = Counts((Verdict.Reached, 10), (Verdict.Orphaned, 6), (Verdict.Unreferenced, 1), (Verdict.Maybe, 1), (Verdict.DevOnly, 2));
        Verdict[] reported = [Verdict.Orphaned, Verdict.Orphaned, Verdict.Unreferenced, Verdict.Maybe];
        var r = ResultText.ForPlugin("MyMod.esm", counts, reported, typesGiven: false);
        Assert.Equal((3, 4, 6), (r.Dead, r.LeftOutDead, r.LeftOut));
        Assert.Contains("adds 20 records, and nothing in the game reaches 3 of them", r.Text);
        Assert.Contains("leaves out 4 structure records", r.Text);
    }

    [Fact]
    public void ForPlugin_NothingLeftOut_ReachesAll()
    {
        var r = ResultText.ForPlugin("MyMod.esm", Counts((Verdict.Reached, 4), (Verdict.Maybe, 1)), [Verdict.Maybe], typesGiven: false);
        Assert.Equal((0, 0, 0), (r.Dead, r.LeftOutDead, r.LeftOut));
        Assert.Contains("the game reaches all of them. 1 is reached only through", r.Text);
    }

    // C7 and code review round 2: an option without its value, at the end or followed by another option, is a usage error.
    [Theory]
    [InlineData(new[] { "scan", "--types" }, 1)]
    [InlineData(new[] { "scan", "--types", "--all-types" }, 1)]
    [InlineData(new[] { "scan", "--plugin", "--verbose" }, 1)]
    public void OptionWithoutValue_IsUsageError(string[] args, int at)
    {
        var i = at;
        var e = Assert.Throws<UsageException>(() => Options.Value(args, ref i));
        Assert.Equal($"{args[at]} needs a value", e.Message);
    }

    [Fact]
    public void OptionValue_TakesTheNextArgument()
    {
        string[] args = ["scan", "--types", "WEAP,ARMO", "--all-types"];
        var i = 1;
        Assert.Equal("WEAP,ARMO", Options.Value(args, ref i));
        Assert.Equal(2, i);
    }

    [Theory]
    [InlineData("SamCoe", true)]
    [InlineData("Neon01", true)]
    [InlineData("Convo_Neon", true)]
    [InlineData("sam", true)]
    [InlineData("Coe Neon", false)]   // not consecutive: "convo" sits between them
    [InlineData("Sam_Neon", false)]
    [InlineData("", false)]
    [InlineData("_", false)]
    [InlineData("Neon02", false)]     // code review round 1: the digits were dropped, so this matched Neon01
    [InlineData("02", false)]
    [InlineData("01", true)]
    [InlineData("samcoe", true)]      // capital letters don't matter
    [InlineData("SAMCOE", true)]
    [InlineData("Neo", false)]        // whole words only
    [InlineData("Neon0", false)]
    public void WordQuery_MatchesRunsOfWholeWords(string query, bool expected) =>
        Assert.Equal(expected, Rules.HasWords("COM_Companion_SamCoe_Convo_Neon01", query));

    [Theory]
    [InlineData("SamCoe", true)]
    [InlineData("samcoe", true)]
    [InlineData("Sam", false)]        // an all-caps word stays one word
    public void WordQuery_AllCapsEditorId(string query, bool expected) =>
        Assert.Equal(expected, Rules.HasWords("SAMCOE_Quest", query));

    [Fact]
    public void WordKey_IsNullWithoutLettersOrDigits()
    {
        Assert.Null(Rules.WordKey("__"));
        Assert.Equal("samcoe01", Rules.WordKey("Sam_Coe01"));
    }
}
