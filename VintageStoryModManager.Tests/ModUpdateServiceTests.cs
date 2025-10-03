using System;
using System.IO;
using System.Threading.Tasks;
using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ModUpdateServiceTests : IAsyncLifetime
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "IMMTests", Guid.NewGuid().ToString("N"));

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_tempDirectory);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup failures in tests.
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task UpdateAsync_ReturnsFailure_ForInvalidArchive()
    {
        string invalidPackagePath = Path.Combine(_tempDirectory, "invalid.zip");
        await File.WriteAllTextAsync(invalidPackagePath, "not a valid archive");

        string targetPath = Path.Combine(_tempDirectory, "mod.zip");
        var descriptor = new ModUpdateDescriptor(
            ModId: "testmod",
            DownloadUri: new Uri(invalidPackagePath),
            TargetPath: targetPath,
            TargetIsDirectory: false,
            ReleaseFileName: Path.GetFileName(invalidPackagePath));

        var service = new ModUpdateService();
        ModUpdateResult result = await service.UpdateAsync(descriptor);

        Assert.False(result.Success);
        Assert.False(File.Exists(targetPath));
        Assert.NotNull(result.ErrorMessage);
    }
}
