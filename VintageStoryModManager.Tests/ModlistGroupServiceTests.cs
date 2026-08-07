using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ModlistGroupServiceTests
{
    [Fact]
    public void DuplicateModAutomaticallySelectsLatestVersion()
    {
        var definition = new ModlistGroupDefinition { Name = "Adventure" };
        var sources = new[]
        {
            CreateSource("one.json", "First", "shared", "1.0.0", false),
            CreateSource("two.json", "Second", "shared", "2.0.0", true)
        };

        var result = ModlistGroupService.BuildMergedPreset(definition, sources);

        var conflict = Assert.Single(result.Conflicts);
        Assert.True(conflict.IsResolved);
        Assert.Equal("shared", conflict.ModId);
        Assert.Equal("two.json", conflict.SelectedMemberReference);
        Assert.Equal("2.0.0", result.MergedPreset.Mods![0].Version);
        Assert.Equal("two.json", Assert.Single(result.MergedPreset.Configurations!).Content);
        Assert.False(conflict.Choices[0].HasCustomConfiguration);
        Assert.True(conflict.Choices[1].HasCustomConfiguration);
    }

    [Fact]
    public void HighestVersionTiePrefersEntryWithConfiguration()
    {
        var sources = new[]
        {
            CreateSource("one.json", "First", "shared", "2.10.0", false),
            CreateSource("two.json", "Second", "shared", "2.10", true),
            CreateSource("three.json", "Third", "shared", "2.9.9", true)
        };

        var result = ModlistGroupService.BuildMergedPreset(new ModlistGroupDefinition(), sources);

        var conflict = Assert.Single(result.Conflicts);
        Assert.Equal("two.json", conflict.SelectedMemberReference);
        Assert.Equal("2.10", Assert.Single(result.MergedPreset.Mods!).Version);
        Assert.Equal("two.json", Assert.Single(result.MergedPreset.Configurations!).Content);
    }

    [Fact]
    public void ResolutionSelectsVersionAndConfigurationFromChosenModlist()
    {
        var definition = new ModlistGroupDefinition
        {
            Name = "Adventure",
            ConflictResolutions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["shared"] = "two.json"
            }
        };
        var sources = new[]
        {
            CreateSource("one.json", "First", "shared", "1.0.0", false),
            CreateSource("two.json", "Second", "shared", "2.0.0", true)
        };

        var result = ModlistGroupService.BuildMergedPreset(definition, sources);

        Assert.True(Assert.Single(result.Conflicts).IsResolved);
        Assert.Equal("2.0.0", Assert.Single(result.MergedPreset.Mods!).Version);
        Assert.Equal("two.json", Assert.Single(result.MergedPreset.Configurations!).Content);
        Assert.True(result.MergedPreset.IncludeModVersions);
    }

    [Fact]
    public void SameVersionWithoutConfigurationsDoesNotCreateConflictAndFirstEntryWins()
    {
        var definition = new ModlistGroupDefinition
        {
            ConflictResolutions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["shared"] = "two.json"
            }
        };
        var sources = new[]
        {
            CreateSource("one.json", "First", "shared", "1.0.0", false),
            CreateSource("two.json", "Second", "shared", "1.0.0", false)
        };
        sources[0].Preset.Mods![0].IsActive = true;
        sources[1].Preset.Mods![0].IsActive = false;

        var result = ModlistGroupService.BuildMergedPreset(definition, sources);

        Assert.Empty(result.Conflicts);
        var selectedMod = Assert.Single(result.MergedPreset.Mods!);
        Assert.Equal("1.0.0", selectedMod.Version);
        Assert.True(selectedMod.IsActive);
        Assert.Null(result.MergedPreset.Configurations);
    }

    [Fact]
    public void ConflictChoicesShowOnlyFirstEntryForSameVersionWithoutConfiguration()
    {
        var definition = new ModlistGroupDefinition
        {
            ConflictResolutions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["shared"] = "two.json"
            }
        };
        var sources = new[]
        {
            CreateSource("one.json", "First", "shared", "1.0.0", false),
            CreateSource("two.json", "Second", "shared", "1.0.0", false),
            CreateSource("three.json", "Third", "shared", "2.0.0", false)
        };

        var result = ModlistGroupService.BuildMergedPreset(definition, sources);

        var conflict = Assert.Single(result.Conflicts);
        Assert.Collection(
            conflict.Choices,
            choice => Assert.Equal("one.json", choice.MemberReference),
            choice => Assert.Equal("three.json", choice.MemberReference));
        Assert.Equal("one.json", conflict.SelectedMemberReference);
        Assert.True(conflict.IsResolved);
        Assert.Equal("1.0.0", Assert.Single(result.MergedPreset.Mods!).Version);
    }

    [Fact]
    public void SameVersionWithDifferentConfigurationContentsCreatesConflict()
    {
        var first = CreateSource("one.json", "First", "shared", "1.0.0", true);
        var second = CreateSource("two.json", "Second", "shared", "1.0.0", true);

        var result = ModlistGroupService.BuildMergedPreset(
            new ModlistGroupDefinition(),
            [first, second]);

        Assert.Single(result.Conflicts);
    }

    [Fact]
    public void SameVersionWithIdenticalConfigurationContentsDoesNotCreateConflict()
    {
        var first = CreateSource("one.json", "First", "shared", "1.0.0", true);
        var second = CreateSource("two.json", "Second", "shared", "1.0.0", true);
        second.Preset.Configurations![0].Content = first.Preset.Configurations![0].Content;

        var result = ModlistGroupService.BuildMergedPreset(
            new ModlistGroupDefinition(),
            [first, second]);

        Assert.Empty(result.Conflicts);
        Assert.Equal("one.json", Assert.Single(result.MergedPreset.Configurations!).Content);
    }

    [Fact]
    public void ReferencesWithinGroupDirectoryArePortable()
    {
        var directory = Path.Combine(Path.GetTempPath(), "svsm-group-reference-test");
        var groupPath = Path.Combine(directory, "group.svsmgroup");
        var memberPath = Path.Combine(directory, "member.json");

        var reference = ModlistGroupService.CreateReference(groupPath, memberPath);

        Assert.Equal("member.json", reference);
        Assert.Equal(Path.GetFullPath(memberPath), ModlistGroupService.ResolveReference(groupPath, reference));
    }

    private static ModlistGroupSource CreateSource(
        string reference,
        string name,
        string modId,
        string version,
        bool includeConfiguration)
    {
        var preset = new SerializablePreset
        {
            Name = name,
            Mods =
            [
                new SerializablePresetModState
                {
                    ModId = modId,
                    Version = version
                }
            ],
            Configurations = includeConfiguration
                ?
                [
                    new SerializableModConfiguration
                    {
                        ModId = modId,
                        FileName = "config.json",
                        Content = reference
                    }
                ]
                : null
        };

        return new ModlistGroupSource(reference, reference, name, preset);
    }
}
