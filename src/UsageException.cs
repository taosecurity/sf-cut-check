// A mistake on the command line, such as an option given without its value (0.2.1). Program.cs prints the usage
// text and exits 2 for it, instead of the crash message it gives for any other error.
namespace SfCutCheck;

public sealed class UsageException(string message) : Exception(message);
