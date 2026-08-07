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

    [Fact]
    public async Task CachedRefreshSkipsUnchangedSuccessfulSearch()
    {
        var modApiService = new RecordingModApiService();
        var viewModel = new ModBrowserViewModel(modApiService);
        viewModel.SetTabVisibility(true);

        await viewModel.RefreshSearchAsync();
        await viewModel.RefreshSearchAsync(true);

        Assert.Equal(1, modApiService.QueryModsCallCount);
    }

    [Fact]
    public async Task CachedRefreshFetchesAgainAfterSearchChanges()
    {
        var modApiService = new RecordingModApiService();
        var viewModel = new ModBrowserViewModel(modApiService);
        viewModel.SetTabVisibility(true);

        await viewModel.RefreshSearchAsync();
        viewModel.SetTabVisibility(false);
        viewModel.SelectedSide = "client";
        viewModel.SetTabVisibility(true);
        await viewModel.RefreshSearchAsync(true);

        Assert.Equal(2, modApiService.QueryModsCallCount);
    }

    [Fact]
    public async Task LeavingTabClosesDetailsAndIgnoresTheInFlightResponse()
    {
        var detailsCompletion = new TaskCompletionSource<DownloadableMod?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var modApiService = new RecordingModApiService
        {
            GetModHandler = (_, _) => detailsCompletion.Task
        };
        var viewModel = new ModBrowserViewModel(modApiService);
        viewModel.SetTabVisibility(true);
        var selectedMod = new DownloadableModOnList
        {
            ModId = 10,
            Name = "First mod",
            Logo = "https://example.invalid/first-logo.png"
        };

        var openTask = viewModel.OpenModDetailsCommand.ExecuteAsync(selectedMod);
        Assert.True(viewModel.IsModDetailsOpen);
        Assert.Equal(selectedMod, viewModel.SelectedMod);

        viewModel.SetTabVisibility(false);

        Assert.False(viewModel.IsModDetailsOpen);
        Assert.Null(viewModel.SelectedMod);
        Assert.Null(viewModel.ModDetails);
        Assert.Empty(viewModel.ScreenshotUrls);

        detailsCompletion.SetResult(new DownloadableMod
        {
            ModId = selectedMod.ModId,
            Screenshots =
            [
                new DownloadableModScreenshot
                {
                    FileId = 1,
                    MainFile = "https://example.invalid/stale-screenshot.png"
                }
            ]
        });
        await openTask;

        Assert.False(viewModel.IsModDetailsOpen);
        Assert.Null(viewModel.SelectedMod);
        Assert.Null(viewModel.ModDetails);
        Assert.Empty(viewModel.ScreenshotUrls);
    }

    private sealed class RecordingModApiService : IModApiService
    {
        public List<GameVersion> GameVersions { get; set; } = [];

        public int GetGameVersionsCallCount { get; private set; }

        public int QueryModsCallCount { get; private set; }

        public Func<int, CancellationToken, Task<DownloadableMod?>>? GetModHandler { get; init; }

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
            return GetModHandler?.Invoke(modId, cancellationToken)
                   ?? Task.FromResult<DownloadableMod?>(null);
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
