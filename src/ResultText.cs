// The RESULT line of scan --plugin (0.2.1, Round 3 C2): it says which records it counts. The exit code follows the
// counted records (dead); records the run leaves out (structure types, or types not in --types) are named, not hidden.
namespace SfCutCheck;

public static class ResultText
{
    static string P(int n, string one) => $"{n:N0} {one}{(n == 1 ? "" : "s")}";
    static string Count(int n) => n == 0 ? "None is" : n == 1 ? "1 is" : $"{n:N0} are";

    /// <summary>The RESULT line and its counts, from the verdict of every record the plugin adds and the verdicts of
    /// the rows the run reports (code review round 2, 2026-11-27: this arithmetic was in Program.cs, untested).</summary>
    /// <param name="counts">Every record the plugin adds, by verdict (Reached included).</param>
    /// <param name="reported">The verdict of each reported row: the records that aren't Reached and whose type the run checks.</param>
    public static (string Text, int Dead, int LeftOutDead, int LeftOut) ForPlugin(
        string pluginName, IReadOnlyDictionary<Verdict, int> counts, IReadOnlyCollection<Verdict> reported, bool typesGiven)
    {
        var added = counts.Values.Sum();
        var dead = reported.Count(v => v is Verdict.Orphaned or Verdict.Unreferenced);
        var maybe = reported.Count(v => v == Verdict.Maybe);
        var devOnly = reported.Count(v => v == Verdict.DevOnly);
        var leftOutDead = counts.GetValueOrDefault(Verdict.Orphaned) + counts.GetValueOrDefault(Verdict.Unreferenced) - dead;
        var leftOut = added - counts.GetValueOrDefault(Verdict.Reached) - reported.Count;
        return (Build(pluginName, added, dead, leftOutDead, leftOut, maybe, devOnly, typesGiven), dead, leftOutDead, leftOut);
    }

    /// <param name="added">Records the plugin adds.</param>
    /// <param name="dead">Orphaned or Unreferenced records among the ones this run reports.</param>
    /// <param name="leftOutDead">Orphaned or Unreferenced records the run leaves out because of their type.</param>
    /// <param name="leftOut">Records that aren't Reached (any verdict) the run leaves out because of their type. When
    /// it isn't 0, the line says "all of its content records" (or "of the types you chose"), not "all of them", since
    /// the Maybe and test-cell counts that follow cover only the reported records (code review round 2).</param>
    /// <param name="typesGiven">True when --types chose the record types.</param>
    public static string Build(string pluginName, int added, int dead, int leftOutDead, int leftOut, int maybe, int devOnly, bool typesGiven)
    {
        var leftOutText = leftOutDead == 0 ? ""
            : typesGiven
                ? $" The run leaves out {P(leftOutDead, "record")} that nothing in the game reaches, because {(leftOutDead == 1 ? "its type isn't" : "their types aren't")} in --types."
                : $" The run leaves out {P(leftOutDead, "structure record")} that nothing in the game reaches (placed references, cells, keywords and the like). Add --all-types to check {(leftOutDead == 1 ? "it" : "them")}.";
        if (dead != 0)
            return $"RESULT: {pluginName} adds {P(added, "record")}, and nothing in the game reaches {dead:N0} of them. " +
                   "The reports list them as Orphaned or Unreferenced." + leftOutText;
        var all = leftOut == 0 && leftOutDead == 0
            ? "and the game reaches all of them. "
            : typesGiven
                ? "and the game reaches all of the records of the types you chose. "
                : "and the game reaches all of its content records. ";
        return $"RESULT: {pluginName} adds {P(added, "record")}, {all}" +
               $"{Count(maybe)} reached only through a recipe, research project or condition check" +
               (maybe == 0 ? ". " : maybe == 1 ? ", so it's marked Maybe. " : ", so they're marked Maybe. ") +
               $"{Count(devOnly)} reached only from test cells." + leftOutText;
    }
}
