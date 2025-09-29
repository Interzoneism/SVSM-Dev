using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using VintageStoryModManager.Models;
using VintageStoryModManager.Utilities;
using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager.Design;

/// <summary>
/// Creates design-time instances of view models.
/// </summary>
internal static class DesignDataFactory
{
    private static readonly byte[] SampleIcon = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9s8Z5n8AAAAASUVORK5CYII=");

    public static IReadOnlyList<ModListItemViewModel> CreateSampleMods()
    {
        var mods = new List<ModListItemViewModel>
        {
            CreateMod(
                modId: "betterhud",
                name: "Better HUD",
                version: "1.4.2",
                networkVersion: "1.21.0",
                website: "https://example.com/betterhud",
                sourcePath: "Mods/betterhud_v1.4.2.zip",
                location: "Mods/betterhud_v1.4.2.zip",
                sourceKind: ModSourceKind.ZipArchive,
                authors: new[] { "Aurora" },
                contributors: new[] { "Vega" },
                dependencies: new[] { new ModDependencyInfo("game", "1.21.0") },
                description: "Adds additional HUD widgets and customization options.",
                side: "Client",
                requiredOnClient: true,
                requiredOnServer: false,
                isActive: true,
                error: null,
                activationError: null,
                modDbPageUrl: "https://mods.vintagestory.at/betterhud",
                modDbVersion: "1.4.2",
                modDbVsVersion: "1.21.0",
                modDbOneClick: "betterhud@1.4.2",
                modDbTags: new[] { "client", "hud" }),
            CreateMod(
                modId: "expandedstorage",
                name: "Expanded Storage",
                version: "2.0.0",
                networkVersion: "1.21.0",
                website: "https://mods.vintagestory.at/expandedstorage",
                sourcePath: "Mods/expandedstorage",
                location: "Mods/expandedstorage",
                sourceKind: ModSourceKind.Folder,
                authors: new[] { "Epsilon" },
                contributors: Array.Empty<string>(),
                dependencies: new[]
                {
                    new ModDependencyInfo("game", "1.21.0"),
                    new ModDependencyInfo("survival", "1.21.0")
                },
                description: "Improves and rebalances all storage-related blocks and recipes.",
                side: "Both",
                requiredOnClient: true,
                requiredOnServer: true,
                isActive: false,
                error: "Missing dependency: CarryCapacity ≥ 1.3.0",
                activationError: null,
                modDbPageUrl: "https://mods.vintagestory.at/expandedstorage",
                modDbVersion: "2.1.0",
                modDbVsVersion: "1.21.0",
                modDbOneClick: "expandedstorage@2.1.0",
                modDbTags: new[] { "both", "storage", "gameplay" }),
            CreateMod(
                modId: "utilityscripts",
                name: "Utility Scripts",
                version: "0.9.1",
                networkVersion: "1.21.0",
                website: null,
                sourcePath: "Mods/utilityscripts.dll",
                location: "Mods/utilityscripts.dll",
                sourceKind: ModSourceKind.Assembly,
                authors: new[] { "Nova", "Rigel" },
                contributors: Array.Empty<string>(),
                dependencies: new[] { new ModDependencyInfo("game", "1.21.0") },
                description: "Assortment of server utilities and chat commands.",
                side: "Server",
                requiredOnClient: false,
                requiredOnServer: true,
                isActive: true,
                error: null,
                activationError: "Failed to enable scripts due to permission error.",
                modDbPageUrl: "https://mods.vintagestory.at/utilityscripts",
                modDbVersion: "0.9.5",
                modDbVsVersion: "1.21.1",
                modDbOneClick: "utilityscripts@0.9.5",
                modDbTags: new[] { "server", "utility" }),
            CreateMod(
                modId: "worldeditplus",
                name: "World Edit Plus",
                version: "3.5.0",
                networkVersion: "1.21.0",
                website: "https://example.com/worldeditplus",
                sourcePath: "Mods/worldeditplus_v3.5.0.zip",
                location: "Mods/worldeditplus_v3.5.0.zip",
                sourceKind: ModSourceKind.ZipArchive,
                authors: new[] { "Lyra" },
                contributors: new[] { "Orion" },
                dependencies: new[] { new ModDependencyInfo("game", "1.21.0") },
                description: "Advanced world editing tools with region presets and macros.",
                side: "Both",
                requiredOnClient: true,
                requiredOnServer: true,
                isActive: true,
                error: null,
                activationError: null,
                modDbPageUrl: "https://mods.vintagestory.at/worldeditplus",
                modDbVersion: "3.6.0",
                modDbVsVersion: "1.21.1",
                modDbOneClick: "worldeditplus@3.6.0",
                modDbTags: new[] { "both", "worldedit" })
        };

        return mods;
    }

    private static ModListItemViewModel CreateMod(
        string modId,
        string name,
        string? version,
        string? networkVersion,
        string? website,
        string sourcePath,
        string location,
        ModSourceKind sourceKind,
        IReadOnlyList<string> authors,
        IReadOnlyList<string> contributors,
        IReadOnlyList<ModDependencyInfo> dependencies,
        string? description,
        string? side,
        bool? requiredOnClient,
        bool? requiredOnServer,
        bool isActive,
        string? error,
        string? activationError,
        string? modDbPageUrl,
        string? modDbVersion,
        string? modDbVsVersion,
        string? modDbOneClick,
        IReadOnlyList<string>? modDbTags)
    {
        var entry = new ModEntry
        {
            ModId = modId,
            Name = name,
            Version = version,
            NetworkVersion = networkVersion,
            Website = website,
            SourcePath = sourcePath,
            SourceKind = sourceKind,
            Authors = authors,
            Contributors = contributors,
            Dependencies = dependencies,
            Description = description,
            Side = side,
            RequiredOnClient = requiredOnClient,
            RequiredOnServer = requiredOnServer,
            IconBytes = SampleIcon,
            IconDescription = "Sample icon",
            Error = error
        };

        var viewModel = new ModListItemViewModel(entry, isActive, location, (_, _) => Task.FromResult(new ActivationResult(true, null)));

        if (!string.IsNullOrWhiteSpace(activationError))
        {
            SetActivationError(viewModel, activationError);
        }

        if (!string.IsNullOrWhiteSpace(modDbPageUrl) || !string.IsNullOrWhiteSpace(modDbVersion) ||
            !string.IsNullOrWhiteSpace(modDbVsVersion) || !string.IsNullOrWhiteSpace(modDbOneClick) ||
            (modDbTags != null && modDbTags.Count > 0))
        {
            SetModDbInfo(viewModel, modDbPageUrl, modDbVersion, modDbVsVersion, modDbOneClick, modDbTags);
        }

        return viewModel;
    }

    private static void SetActivationError(ModListItemViewModel viewModel, string error)
    {
        var setter = typeof(ModListItemViewModel)
            .GetProperty(nameof(ModListItemViewModel.ActivationError), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?
            .GetSetMethod(true);

        setter?.Invoke(viewModel, new object?[] { error });
    }

    private static void SetModDbInfo(
        ModListItemViewModel viewModel,
        string? pageUrl,
        string? latestVersion,
        string? latestGameVersion,
        string? oneClick,
        IReadOnlyList<string>? tags)
    {
        var method = typeof(ModListItemViewModel)
            .GetMethod("ApplyModDbInfo", BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
        {
            return;
        }

        var info = new VintageStoryModDb.ModDbEntryInfo
        {
            ModPageUrl = pageUrl ?? string.Empty,
            ModIdOrSlug = viewModel.ModId,
            DisplayName = viewModel.DisplayName,
            LatestModVersion = latestVersion ?? string.Empty,
            LatestCompatibleGameVersion = latestGameVersion ?? string.Empty,
            OneClickInstallUrl = oneClick ?? string.Empty,
            Tags = tags?.Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag.Trim()).ToList()
                ?? new List<string>()
        };

        method.Invoke(viewModel, new object?[] { info });
    }
}
