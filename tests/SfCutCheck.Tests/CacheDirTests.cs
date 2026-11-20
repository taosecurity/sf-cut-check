// The saved-scan folder after the 0.1.9 rename: SF_CUT_CHECK_CACHE_DIR first, the old SFCUTCHECK_CACHE_DIR second,
// then %LOCALAPPDATA%\sf-cut-check. No other test reads these variables, so setting them here is safe.
using Xunit;

namespace SfCutCheck.Tests;

public sealed class CacheDirTests
{
    const string New = "SF_CUT_CHECK_CACHE_DIR", Old = "SFCUTCHECK_CACHE_DIR";

    static string With(string? newValue, string? oldValue)
    {
        var (keepNew, keepOld) = (Environment.GetEnvironmentVariable(New), Environment.GetEnvironmentVariable(Old));
        try
        {
            Environment.SetEnvironmentVariable(New, newValue);
            Environment.SetEnvironmentVariable(Old, oldValue);
            return OfficialCache.DefaultDir;
        }
        finally
        {
            Environment.SetEnvironmentVariable(New, keepNew);
            Environment.SetEnvironmentVariable(Old, keepOld);
        }
    }

    [Fact]
    public void NewVariableWins() => Assert.Equal(@"C:\a", With(@"C:\a", @"C:\b"));

    [Fact]
    public void OldVariableStillWorks() => Assert.Equal(@"C:\b", With(null, @"C:\b"));

    [Fact]
    public void DefaultIsTheNewFolder()
    {
        var d = With(null, null);
        Assert.Equal("sf-cut-check", Path.GetFileName(d));
        Assert.Equal(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Path.GetDirectoryName(d));
    }
}
