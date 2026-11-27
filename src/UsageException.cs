// A mistake on the command line, such as an option given without its value (0.2.1). Program.cs prints the usage
// text and exits 2 for it, instead of the crash message it gives for any other error.
namespace SfCutCheck;

public sealed class UsageException(string message) : Exception(message);

public static class Options
{
    /// <summary>The value after the option at args[i] (and i moves onto it). An option at the end of the command, or
    /// one followed by another option (`--types --all-types`), has no value: that's a UsageException (code review
    /// round 2, 2026-11-27).</summary>
    public static string Value(string[] args, ref int i) =>
        i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)
            ? args[++i]
            : throw new UsageException($"{args[i]} needs a value");
}
