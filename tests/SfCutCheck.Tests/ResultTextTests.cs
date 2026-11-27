// 0.2.1 (Round 3): C2 the RESULT line names what it leaves out, C5 find --word with a multi-word query.
using SfCutCheck;
using Xunit;

public class ResultTextTests
{
    [Fact]
    public void LeftOutStructureRecordsAreNamed_NotReachesAll()
    {
        // 45 records, 37 of them unlinked structure records the default types leave out; the 8 content records are reached.
        var s = ResultText.Build("MyMod.esm", 45, 0, 37, 0, 0, typesGiven: false);
        Assert.DoesNotContain("reaches all of them", s);
        Assert.Contains("all of its content records", s);
        Assert.Contains("leaves out 37 structure records that nothing in the game reaches", s);
        Assert.Contains("--all-types", s);
    }

    [Fact]
    public void NothingLeftOut_KeepsTheOldSentence()
    {
        var s = ResultText.Build("MyMod.esm", 3, 0, 0, 1, 0, typesGiven: false);
        Assert.StartsWith("RESULT: MyMod.esm adds 3 records, and the game reaches all of them. 1 is reached only through", s);
        Assert.DoesNotContain("leaves out", s);
    }

    [Fact]
    public void DeadRecords_StillCountedAndLeftOutStillNamed()
    {
        var s = ResultText.Build("MyMod.esm", 90, 7, 70, 0, 0, typesGiven: false);
        Assert.Contains("nothing in the game reaches 7 of them", s);
        Assert.Contains("leaves out 70 structure records", s);
    }

    [Fact]
    public void WithTypes_SaysTheTypesAreTheReason()
    {
        var s = ResultText.Build("MyMod.esm", 10, 0, 1, 0, 0, typesGiven: true);
        Assert.Contains("records of the types you chose", s);
        Assert.Contains("leaves out 1 record that nothing in the game reaches, because its type isn't in --types", s);
    }

    [Theory]
    [InlineData("SamCoe", true)]
    [InlineData("Neon01", true)]
    [InlineData("Convo_Neon", true)]
    [InlineData("sam", true)]
    [InlineData("Coe Neon", false)]   // not consecutive: "convo" sits between them
    [InlineData("Sam_Neon", false)]
    [InlineData("", false)]
    public void WordQuery_MatchesRunsOfWholeWords(string query, bool expected) =>
        Assert.Equal(expected, Rules.HasWords("COM_Companion_SamCoe_Convo_Neon01", query));
}
