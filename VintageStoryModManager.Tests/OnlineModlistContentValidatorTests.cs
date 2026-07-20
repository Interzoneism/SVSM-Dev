using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class OnlineModlistContentValidatorTests
{
    [Theory]
    [InlineData("A bitch of a modlist", "bitch")]
    [InlineData("Includes a BLOW\tJOB joke", "blow job")]
    [InlineData("twat!", "twat")]
    public void TryFindBlockedWord_ReturnsMatchingPackagedEntry(string text, string expectedWord)
    {
        var wasBlocked = OnlineModlistContentValidator.TryFindBlockedWord(new[] { text }, out var blockedWord);

        Assert.True(wasBlocked);
        Assert.Equal(expectedWord, blockedWord);
    }

    [Theory]
    [InlineData("A classic collection")]
    [InlineData("Analysis helpers")]
    [InlineData("New subtitle pack")]
    public void TryFindBlockedWord_DoesNotMatchInsideLongerWords(string text)
    {
        var wasBlocked = OnlineModlistContentValidator.TryFindBlockedWord(new[] { text }, out var blockedWord);

        Assert.False(wasBlocked);
        Assert.Empty(blockedWord);
    }

    [Fact]
    public void TryFindBlockedWord_ChecksEveryProvidedPublicField()
    {
        var wasBlocked = OnlineModlistContentValidator.TryFindBlockedWord(
            new[] { "Clean name", "Clean description", "queer" },
            out var blockedWord);

        Assert.True(wasBlocked);
        Assert.Equal("queer", blockedWord);
    }
}
