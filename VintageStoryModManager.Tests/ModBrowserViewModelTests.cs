using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ModBrowserViewModelTests
{
    [Fact]
    public async Task LoadAvailableGameVersionsAsyncLoadsVersionsWithoutSearchingMods()
    {
        var modApiService = new RecordingModApiService
        {
            GameVersions =
            [
                new GameVersion { TagId = 2, Name = "1.22.0" },
                new GameVersion { TagId = 1, Name = "1.21.0" }
            ]
        };
        var viewModel = new ModBrowserViewModel(modApiService);

        await viewModel.LoadAvailableGameVersionsAsync();

        Assert.Equal(["1.22.0", "1.21.0"], viewModel.AvailableVersions.Select(version => version.Name));
        Assert.Equal(1, modApiService.GetGameVersionsCallCount);
        Assert.Equal(0, modApiService.QueryModsCallCount);
    }

    private sealed class RecordingModApiService : IModApiService
    {
        public List<GameVersion> GameVersions { get; set; } = [];

        public int GetGameVersionsCallCount { get; private set; }

        public int QueryModsCallCount { get; private set; }

        public Task<List<DownloadableModOnList>> QueryModsAsync(
            string? textFilter = null,
            ModAuthor? authorFilter = null,
            IEnumerable<GameVersion>? versionsFilter = null,
            IEnumerable<ModTag>? tagsFilter = null,
            string orderBy = "follows",
            string orderByOrder = "desc",
            CancellationToken cancellationToken = default)
        {
            QueryModsCallCount++;
            return Task.FromResult(new List<DownloadableModOnList>());
        }

        public Task<DownloadableMod?> GetModAsync(int modId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<DownloadableMod?>(null);
        }

        public Task<DownloadableMod?> GetModAsync(string modIdStr, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<DownloadableMod?>(null);
        }

        public Task<List<ModAuthor>> GetAuthorsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<ModAuthor>());
        }

        public Task<List<GameVersion>> GetGameVersionsAsync(CancellationToken cancellationToken = default)
        {
            GetGameVersionsCallCount++;
            return Task.FromResult(GameVersions);
        }

        public Task<List<ModTag>> GetTagsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<ModTag>());
        }

        public Task<bool> DownloadModAsync(
            string downloadUrl,
            string destinationPath,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }
    }
}
