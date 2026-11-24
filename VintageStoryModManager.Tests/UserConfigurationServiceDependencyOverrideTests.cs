using System.ComponentModel;
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

    [Fact]
    public void CacheModDatabaseResultsDefaultsOffAndPersistsWhenEnabled()
    {
        var configuration = new UserConfigurationService();

        Assert.False(configuration.CacheModDatabaseResults);

        configuration.EnablePersistence();
        configuration.SetCacheModDatabaseResults(true);

        var reloaded = new UserConfigurationService();
        Assert.True(reloaded.CacheModDatabaseResults);
    }

    [Fact]
    public void CloseManagerAfterGameLaunchDefaultsOffAndPersistsWhenEnabled()
    {
        var configuration = new UserConfigurationService();

        Assert.False(configuration.CloseManagerAfterGameLaunch);

        configuration.EnablePersistence();
        configuration.SetCloseManagerAfterGameLaunch(true);

        var reloaded = new UserConfigurationService();
        Assert.True(reloaded.CloseManagerAfterGameLaunch);
    }

    [Fact]
    public void ModInfoPanePlacementDefaultsRightAndPersistsWhenMovedLeft()
    {
        var configuration = new UserConfigurationService();

        Assert.Equal(ModInfoPanePlacement.Right, configuration.ModInfoPanePlacement);

        configuration.EnablePersistence();
        configuration.SetModInfoPanePlacement(ModInfoPanePlacement.Left);

        var reloaded = new UserConfigurationService();
        Assert.Equal(ModInfoPanePlacement.Left, reloaded.ModInfoPanePlacement);
    }

    [Fact]
    public void LocalAndOnlineModlistSortPreferencesPersistIndependently()
    {
        var configuration = new UserConfigurationService();

        Assert.Equal((null, ListSortDirection.Ascending), configuration.GetLocalModlistsSortPreference());
        Assert.Equal((null, ListSortDirection.Ascending), configuration.GetOnlineModlistsSortPreference());

        configuration.EnablePersistence();
        configuration.SetLocalModlistsSortPreference("LastModified", ListSortDirection.Descending);
        configuration.SetOnlineModlistsSortPreference("DisplayName", ListSortDirection.Ascending);

        var reloaded = new UserConfigurationService();

        Assert.Equal(("LastModified", ListSortDirection.Descending),
            reloaded.GetLocalModlistsSortPreference());
        Assert.Equal(("DisplayName", ListSortDirection.Ascending),
            reloaded.GetOnlineModlistsSortPreference());
    }

    [Fact]
    public void HiddenOnlineModlistsAndAuthorsPersistAndCanBeClearedIndependently()
    {
        var configuration = new UserConfigurationService();
        configuration.EnablePersistence();

        Assert.True(configuration.HideOnlineModlist("owner-one", "slot-a"));
        Assert.True(configuration.HideOnlineModlistAuthor("Creator Name"));

        var reloaded = new UserConfigurationService();
        Assert.True(reloaded.IsOnlineModlistHidden("OWNER-ONE", "SLOT-A"));
        Assert.True(reloaded.IsOnlineModlistAuthorHidden("creator name"));

        Assert.True(reloaded.ClearHiddenOnlineModlists());
        Assert.False(reloaded.IsOnlineModlistHidden("owner-one", "slot-a"));
        Assert.True(reloaded.IsOnlineModlistAuthorHidden("creator name"));

        Assert.True(reloaded.ClearHiddenOnlineModlistAuthors());
        Assert.False(reloaded.IsOnlineModlistAuthorHidden("creator name"));
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
