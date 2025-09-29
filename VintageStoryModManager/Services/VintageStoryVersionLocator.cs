using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace VintageStoryModManager.Services;

/// <summary>
/// Attempts to determine the installed Vintage Story version for compatibility checks.
/// </summary>
public static class VintageStoryVersionLocator
{
    private static readonly string[] CandidateFiles =
    {
        "VintagestoryAPI.dll",
        Path.Combine("VSFOLDER", "VintagestoryAPI.dll")
    };

    public static string? GetInstalledVersion()
    {
        string? fromEnvironment = VersionStringUtility.Normalize(Environment.GetEnvironmentVariable("VINTAGE_STORY_VERSION"));
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment;
        }

        foreach (string candidate in EnumerateCandidates())
        {
            string? version = TryGetVersionFromFile(candidate);
            if (!string.IsNullOrWhiteSpace(version))
            {
                return version;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateCandidates()
    {
        string? explicitFolder = Environment.GetEnvironmentVariable("VINTAGE_STORY");
        if (!string.IsNullOrWhiteSpace(explicitFolder))
        {
            foreach (string file in CandidateFiles)
            {
                yield return Path.Combine(explicitFolder!, file);
            }
        }

        string baseDirectory = AppContext.BaseDirectory;
        foreach (string file in CandidateFiles)
        {
            yield return Path.Combine(baseDirectory, file);
        }
    }

    private static string? TryGetVersionFromFile(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
            string? fromFileVersion = VersionStringUtility.Normalize(info.FileVersion);
            if (!string.IsNullOrWhiteSpace(fromFileVersion))
            {
                return fromFileVersion;
            }

            string? fromProductVersion = VersionStringUtility.Normalize(info.ProductVersion);
            if (!string.IsNullOrWhiteSpace(fromProductVersion))
            {
                return fromProductVersion;
            }

            AssemblyName assembly = AssemblyName.GetAssemblyName(path);
            return VersionStringUtility.Normalize(assembly.Version?.ToString());
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (BadImageFormatException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
