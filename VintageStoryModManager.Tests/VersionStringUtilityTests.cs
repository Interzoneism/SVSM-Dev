using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class VersionStringUtilityTests
{
    [Theory]
    [InlineData("1.0.1", "1.0.0", true)]
    [InlineData("1.2", "1.2", false)]
    [InlineData("1.2.0", "1.2", false)]
    [InlineData("1.2.1", "1.2", true)]
    [InlineData("v2.0.0-beta", "1.9.9", true)]
    [InlineData(null, "1.0.0", false)]
    [InlineData("1.0.0", null, true)]
    [InlineData("1.0", "1.0.1", false)]
    public void IsCandidateVersionNewer_ProducesExpectedResults(string? candidate, string? current, bool expected)
    {
        bool result = VersionStringUtility.IsCandidateVersionNewer(candidate, current);
        Assert.Equal(expected, result);
    }
}
