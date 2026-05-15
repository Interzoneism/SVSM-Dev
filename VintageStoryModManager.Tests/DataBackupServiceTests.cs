using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class DataBackupServiceTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "svsm-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task PruneBackupsKeepsNewestMatchingDataFolderAndVersion()
    {
        var configurationDirectory = Path.Combine(_tempDirectory, "config");
        var backupDirectory = Path.Combine(_tempDirectory, "backups");
        var dataDirectory = Path.Combine(_tempDirectory, "VintagestoryData");
        var otherDataDirectory = Path.Combine(_tempDirectory, "OtherData");
        var service = new DataBackupService(configurationDirectory, backupDirectory);

        var oldBackup = await CreateBackupAsync(service, dataDirectory, "1.20.4", "old");
        var middleBackup = await CreateBackupAsync(service, dataDirectory, "1.20.4", "middle");
        var newestBackup = await CreateBackupAsync(service, dataDirectory, "1.20.4", "newest");
        var otherVersionBackup = await CreateBackupAsync(service, dataDirectory, "1.20.5", "other-version");
        var otherFolderBackup = await CreateBackupAsync(service, otherDataDirectory, "1.20.4", "other-folder");

        var deleted = service.PruneBackups(dataDirectory, "1.20.4", 2);

        Assert.Equal(1, deleted);
        Assert.False(BackupExists(oldBackup));
        Assert.True(BackupExists(middleBackup));
        Assert.True(BackupExists(newestBackup));
        Assert.True(BackupExists(otherVersionBackup));
        Assert.True(BackupExists(otherFolderBackup));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    private static async Task<DataBackupResult> CreateBackupAsync(
        DataBackupService service,
        string dataDirectory,
        string version,
        string content)
    {
        Directory.CreateDirectory(dataDirectory);
        await File.WriteAllTextAsync(Path.Combine(dataDirectory, "clientsettings.json"), content).ConfigureAwait(false);
        return await service
            .CreateBackupAsync(dataDirectory, version, null, CancellationToken.None)
            .ConfigureAwait(false);
    }

    private static bool BackupExists(DataBackupResult result)
    {
        return Directory.Exists(result.DirectoryPath) || File.Exists(result.DirectoryPath);
    }
}
