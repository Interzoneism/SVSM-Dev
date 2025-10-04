using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

/// <summary>
/// Downloads and installs mod updates retrieved from the official mod database.
/// </summary>
public sealed class ModUpdateService
{
    private static readonly HttpClient HttpClient = new();

    private sealed record DownloadResult(string Path, bool IsTemporary, string? CachePath, bool IsCacheHit);

    public async Task<ModUpdateResult> UpdateAsync(ModUpdateDescriptor descriptor, IProgress<ModUpdateProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (descriptor is null)
        {
            throw new ArgumentNullException(nameof(descriptor));
        }

        try
        {
            ReportProgress(progress, ModUpdateStage.Downloading, "Downloading update package...");
            DownloadResult download = await DownloadAsync(descriptor, cancellationToken).ConfigureAwait(false);

            try
            {
                ReportProgress(progress, ModUpdateStage.Validating, "Validating archive...");
                ValidateArchive(download.Path);

                if (!download.IsCacheHit && download.CachePath != null)
                {
                    TryCacheDownload(download.Path, download.CachePath);
                }

                bool treatAsDirectory = descriptor.TargetIsDirectory;
                return await InstallAsync(descriptor, download.Path, treatAsDirectory, progress, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (download.IsTemporary)
                {
                    TryDelete(download.Path);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Trace.TraceWarning("Mod update cancelled for {0}", descriptor.TargetPath);
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or HttpRequestException or NotSupportedException)
        {
            Trace.TraceError("Mod update failed for {0}: {1}", descriptor.TargetPath, ex);
            return new ModUpdateResult(false, ex.Message);
        }
    }

    private static async Task<DownloadResult> DownloadAsync(ModUpdateDescriptor descriptor, CancellationToken cancellationToken)
    {
        string? fileName = descriptor.ReleaseFileName;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = descriptor.TargetIsDirectory ? descriptor.ModId + ".zip" : Path.GetFileName(descriptor.TargetPath);
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = descriptor.ModId + ".zip";
        }

        string? cachePath = GetCachePath(descriptor, fileName);
        if (cachePath != null && File.Exists(cachePath))
        {
            return new DownloadResult(Path: cachePath, IsTemporary: false, CachePath: cachePath, IsCacheHit: true);
        }

        string tempDirectory = CreateTemporaryDirectory();
        string downloadPath = Path.Combine(tempDirectory, fileName);

        if (descriptor.DownloadUri.IsFile)
        {
            string sourcePath = descriptor.DownloadUri.LocalPath;
            await using FileStream sourceStream = File.OpenRead(sourcePath);
            await using FileStream destination = File.Create(downloadPath);
            await sourceStream.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            using HttpRequestMessage request = new(HttpMethod.Get, descriptor.DownloadUri);
            using HttpResponseMessage response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using FileStream destination = File.Create(downloadPath);
            await stream.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        }

        return new DownloadResult(Path: downloadPath, IsTemporary: true, CachePath: cachePath, IsCacheHit: false);
    }

    private static string? GetCachePath(ModUpdateDescriptor descriptor, string fileName)
    {
        if (string.IsNullOrWhiteSpace(descriptor.ReleaseVersion))
        {
            return null;
        }

        string? documentsDirectory = GetDocumentsDirectory();
        if (string.IsNullOrWhiteSpace(documentsDirectory))
        {
            return null;
        }

        string modDirectory = SanitizeFileName(descriptor.ModId, "mod");
        string versionSegment = SanitizeFileName(descriptor.ReleaseVersion!, "version");
        string extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".zip";
        }

        string cacheDirectory = Path.Combine(documentsDirectory, "VS Mod Manager", "Cached Mods", modDirectory);
        return Path.Combine(cacheDirectory, versionSegment + extension);
    }

    private static string? GetDocumentsDirectory()
    {
        string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        path = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        return string.IsNullOrWhiteSpace(path) ? null : path;
    }

    private static string SanitizeFileName(string? input, string fallback)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return fallback;
        }

        char[] invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(input.Length);
        foreach (char c in input)
        {
            if (Array.IndexOf(invalidChars, c) >= 0)
            {
                builder.Append('_');
            }
            else
            {
                builder.Append(c);
            }
        }

        string sanitized = builder.ToString().Trim();
        return string.IsNullOrEmpty(sanitized) ? fallback : sanitized;
    }

    private static void ValidateArchive(string downloadPath)
    {
        try
        {
            using ZipArchive archive = ZipFile.OpenRead(downloadPath);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (string.Equals(Path.GetFileName(entry.FullName), "modinfo.json", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidDataException("The downloaded file is not a valid Vintage Story mod archive.", ex);
        }

        throw new InvalidDataException("The downloaded file does not contain a modinfo.json manifest.");
    }

    private static Task<ModUpdateResult> InstallAsync(ModUpdateDescriptor descriptor, string downloadPath, bool treatAsDirectory, IProgress<ModUpdateProgress>? progress, CancellationToken cancellationToken)
    {
        string targetPath = descriptor.TargetPath;

        if (treatAsDirectory)
        {
            ReportProgress(progress, ModUpdateStage.Preparing, "Preparing extracted files...");
            ModUpdateResult result = InstallToDirectory(targetPath, downloadPath, progress, cancellationToken);
            return Task.FromResult(result);
        }

        ReportProgress(progress, ModUpdateStage.Replacing, "Replacing mod archive...");
        InstallToFile(targetPath, downloadPath);
        ReportProgress(progress, ModUpdateStage.Completed, "Update installed.");
        return Task.FromResult(new ModUpdateResult(true, null));
    }

    private static ModUpdateResult InstallToDirectory(string targetDirectory, string downloadPath, IProgress<ModUpdateProgress>? progress, CancellationToken cancellationToken)
    {
        string backupPath = CreateUniquePath(targetDirectory, ".immbackup");
        string extractDirectory = CreateTemporaryDirectory();

        bool backupMoved = false;

        try
        {
            if (Directory.Exists(targetDirectory))
            {
                if (Directory.Exists(backupPath))
                {
                    Directory.Delete(backupPath, true);
                }

                Directory.Move(targetDirectory, backupPath);
                backupMoved = true;
            }

            ZipFile.ExtractToDirectory(downloadPath, extractDirectory);

            string payloadRoot = DeterminePayloadRoot(extractDirectory);
            CopyDirectory(payloadRoot, targetDirectory, cancellationToken);
            ReportProgress(progress, ModUpdateStage.Completed, "Update installed.");

            TryDelete(backupPath);
            return new ModUpdateResult(true, null);
        }
        catch (OperationCanceledException)
        {
            TryDelete(targetDirectory);
            if (backupMoved)
            {
                TryRestoreDirectoryBackup(backupPath, targetDirectory);
            }

            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            TryDelete(targetDirectory);
            if (backupMoved)
            {
                TryRestoreDirectoryBackup(backupPath, targetDirectory);
            }

            return new ModUpdateResult(false, ex.Message);
        }
        catch
        {
            TryDelete(targetDirectory);
            if (backupMoved)
            {
                TryRestoreDirectoryBackup(backupPath, targetDirectory);
            }

            throw;
        }
        finally
        {
            TryDelete(extractDirectory);
        }
    }

    private static void InstallToFile(string targetPath, string downloadPath)
    {
        string? directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string backupPath = CreateUniquePath(targetPath, ".immbackup");
        if (File.Exists(targetPath))
        {
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }

            File.Move(targetPath, backupPath);
        }

        try
        {
            File.Copy(downloadPath, targetPath, true);
            TryDelete(backupPath);
        }
        catch (Exception)
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }

            if (File.Exists(backupPath))
            {
                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }

                File.Move(backupPath, targetPath);
            }

            throw;
        }
    }

    private static string DeterminePayloadRoot(string extractDirectory)
    {
        string[] directories = Directory.GetDirectories(extractDirectory, "*", SearchOption.TopDirectoryOnly);
        string[] files = Directory.GetFiles(extractDirectory, "*", SearchOption.TopDirectoryOnly);

        if (directories.Length == 1 && files.Length == 0)
        {
            return directories[0];
        }

        return extractDirectory;
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory, CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(sourceDirectory);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string current = pending.Pop();
            string relative = Path.GetRelativePath(sourceDirectory, current);
            string target = relative == "." ? destinationDirectory : Path.Combine(destinationDirectory, relative);

            Directory.CreateDirectory(target);

            foreach (string file in Directory.GetFiles(current))
            {
                string fileRelative = Path.GetRelativePath(sourceDirectory, file);
                string targetFile = Path.Combine(destinationDirectory, fileRelative);
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
                File.Copy(file, targetFile, true);
            }

            foreach (string directory in Directory.GetDirectories(current))
            {
                pending.Push(directory);
            }
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "IMM", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateUniquePath(string basePath, string suffix)
    {
        string candidate = basePath + suffix;
        int counter = 1;
        while (File.Exists(candidate) || Directory.Exists(candidate))
        {
            candidate = basePath + suffix + counter++;
        }

        return candidate;
    }

    private static void TryCacheDownload(string sourcePath, string cachePath)
    {
        try
        {
            if (string.Equals(sourcePath, cachePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string? directory = Path.GetDirectoryName(cachePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy(sourcePath, cachePath, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            Trace.TraceWarning("Failed to cache mod archive {0}: {1}", cachePath, ex.Message);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            else if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning("Failed to delete temporary resource {0}: {1}", path, ex.Message);
        }
    }

    private static void TryRestoreDirectoryBackup(string backupPath, string targetPath)
    {
        if (!Directory.Exists(backupPath))
        {
            return;
        }

        try
        {
            if (Directory.Exists(targetPath))
            {
                Directory.Delete(targetPath, true);
            }

            Directory.Move(backupPath, targetPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning("Failed to restore mod directory backup {0}: {1}", backupPath, ex.Message);
        }
    }

    private static void ReportProgress(IProgress<ModUpdateProgress>? progress, ModUpdateStage stage, string message)
    {
        progress?.Report(new ModUpdateProgress(stage, message));
    }
}

public sealed record ModUpdateDescriptor(
    string ModId,
    Uri DownloadUri,
    string TargetPath,
    bool TargetIsDirectory,
    string? ReleaseFileName,
    string? ReleaseVersion);

public sealed record ModUpdateResult(bool Success, string? ErrorMessage);

public readonly record struct ModUpdateProgress(ModUpdateStage Stage, string Message);

public enum ModUpdateStage
{
    Downloading,
    Validating,
    Preparing,
    Replacing,
    Completed
}

