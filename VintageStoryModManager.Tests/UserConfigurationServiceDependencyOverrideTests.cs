using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class UserConfigurationServiceDependencyOverrideTests : IDisposable
{
    private readonly string _tempDirectory;

    public UserConfigurationServiceDependencyOverrideTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "svsm-config-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        CustomConfigFolderManager.SetCustomConfigFolder(_tempDirectory);
    }

    [Fact]
    public void DependencyWarningOverridePersistsForModVersion()
    {
        var configuration = new UserConfigurationService();
        configuration.EnablePersistence();

        configuration.SetDependencyWarningOverride("Carry On", "1.2.3", true);

        var reloaded = new UserConfigurationService();

        Assert.True(reloaded.IsDependencyWarningOverridden("carry on", "1.2.3"));
        Assert.False(reloaded.IsDependencyWarningOverridden("carry on", "1.2.4"));
    }

    [Fact]
    public void PruneDependencyWarningOverridesRemovesUpdatedAndRemovedMods()
    {
        var configuration = new UserConfigurationService();
        configuration.EnablePersistence();
        configuration.SetDependencyWarningOverride("kept", "1.0.0", true);
        configuration.SetDependencyWarningOverride("updated", "1.0.0", true);
        configuration.SetDependencyWarningOverride("removed", "1.0.0", true);

        configuration.PruneDependencyWarningOverrides(new (string ModId, string? Version)[]
        {
            ("kept", "1.0.0"),
            ("updated", "2.0.0")
        });

        Assert.True(configuration.IsDependencyWarningOverridden("kept", "1.0.0"));
        Assert.False(configuration.IsDependencyWarningOverridden("updated", "1.0.0"));
        Assert.False(configuration.IsDependencyWarningOverridden("removed", "1.0.0"));
    }

    public void Dispose()
    {
        CustomConfigFolderManager.ClearCustomConfigFolder();

        try
        {
            if (Directory.Exists(_tempDirectory)) Directory.Delete(_tempDirectory, true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
