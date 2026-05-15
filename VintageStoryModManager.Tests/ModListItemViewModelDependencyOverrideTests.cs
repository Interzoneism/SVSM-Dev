using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ModListItemViewModelDependencyOverrideTests
{
    [Fact]
    public void CanToggleWhenDependencyWarningIsOverridden()
    {
        var entry = new ModEntry
        {
            ModId = "dependentmod",
            Name = "Dependent Mod",
            Version = "1.0.0",
            SourceKind = ModSourceKind.ZipArchive,
            LoadError = "Unable to load mod. Requires dependency library v2.0.0",
            MissingDependencies =
            [
                new ModDependencyInfo("library", "2.0.0")
            ]
        };

        var mod = new ModListItemViewModel(
            entry,
            isActive: false,
            location: "Mods",
            activationHandler: (_, _) => Task.FromResult(new ActivationResult(true, null)),
            installedGameVersion: "1.20.0",
            isInstalled: true,
            shouldSkipVersion: null,
            requireExactVersionMatch: () => true,
            initializeUserReportState: false,
            timingService: null,
            isDependencyWarningOverridden: (_, _) => true);

        Assert.True(mod.CanToggle);
    }
}
