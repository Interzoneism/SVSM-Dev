using System.Text.Json.Nodes;
using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ClientSettingsStorePersistenceTests
{
    [Fact]
    public void TrySetActiveRestoresStateAfterWriteFailureAndRetriesSuccessfully()
    {
        using var fixture = ClientSettingsFixture.Create("sample", "sample@1.0.0");
        var originalJson = File.ReadAllText(fixture.Store.SettingsPath);

        using (fixture.LockSettingsFile())
        {
            var success = fixture.Store.TrySetActive("sample", "1.0.0", true, out var error);

            Assert.False(success);
            Assert.Contains(fixture.Store.SettingsPath, error!, StringComparison.OrdinalIgnoreCase);
            Assert.True(fixture.Store.IsDisabled("sample", "1.0.0"));
            Assert.Equal(["sample", "sample@1.0.0"], fixture.Store.DisabledEntries);
        }

        Assert.Equal(originalJson, File.ReadAllText(fixture.Store.SettingsPath));
        Assert.True(fixture.Store.TrySetActive("sample", "1.0.0", true, out var retryError));
        Assert.Null(retryError);
        Assert.False(fixture.Store.IsDisabled("sample", "1.0.0"));
        Assert.Empty(ReadDisabledEntries(fixture.Store.SettingsPath));
    }

    [Fact]
    public void TryApplyDisabledEntriesRestoresStateAfterWriteFailureAndRetriesSuccessfully()
    {
        using var fixture = ClientSettingsFixture.Create("original@1.0.0");
        var originalJson = File.ReadAllText(fixture.Store.SettingsPath);

        using (fixture.LockSettingsFile())
        {
            var success = fixture.Store.TryApplyDisabledEntries(["replacement@2.0.0"], out var error);

            Assert.False(success);
            Assert.Contains(fixture.Store.SettingsPath, error!, StringComparison.OrdinalIgnoreCase);
            Assert.True(fixture.Store.IsDisabled("original", "1.0.0"));
            Assert.False(fixture.Store.IsDisabled("replacement", "2.0.0"));
            Assert.Equal(["original@1.0.0"], fixture.Store.DisabledEntries);
        }

        Assert.Equal(originalJson, File.ReadAllText(fixture.Store.SettingsPath));
        Assert.True(fixture.Store.TryApplyDisabledEntries(["replacement@2.0.0"], out var retryError));
        Assert.Null(retryError);
        Assert.False(fixture.Store.IsDisabled("original", "1.0.0"));
        Assert.True(fixture.Store.IsDisabled("replacement", "2.0.0"));
        Assert.Equal(["replacement@2.0.0"], ReadDisabledEntries(fixture.Store.SettingsPath));
    }

    [Fact]
    public void TryUpdateDisabledEntryRestoresStateAfterWriteFailureAndRetriesSuccessfully()
    {
        using var fixture = ClientSettingsFixture.Create("sample@1.0.0");
        var originalJson = File.ReadAllText(fixture.Store.SettingsPath);

        using (fixture.LockSettingsFile())
        {
            var success = fixture.Store.TryUpdateDisabledEntry(
                "sample",
                "1.0.0",
                "2.0.0",
                shouldDisable: true,
                out var error);

            Assert.False(success);
            Assert.Contains(fixture.Store.SettingsPath, error!, StringComparison.OrdinalIgnoreCase);
            Assert.True(fixture.Store.IsDisabled("sample", "1.0.0"));
            Assert.False(fixture.Store.IsDisabled("sample", "2.0.0"));
            Assert.Equal(["sample@1.0.0"], fixture.Store.DisabledEntries);
        }

        Assert.Equal(originalJson, File.ReadAllText(fixture.Store.SettingsPath));
        Assert.True(fixture.Store.TryUpdateDisabledEntry(
            "sample",
            "1.0.0",
            "2.0.0",
            shouldDisable: true,
            out var retryError));
        Assert.Null(retryError);
        Assert.False(fixture.Store.IsDisabled("sample", "1.0.0"));
        Assert.True(fixture.Store.IsDisabled("sample", "2.0.0"));
        Assert.Equal(["sample@2.0.0"], ReadDisabledEntries(fixture.Store.SettingsPath));
    }

    [Fact]
    public void TrySetActiveDeactivationRemainsIdempotent()
    {
        using var fixture = ClientSettingsFixture.Create();

        Assert.True(fixture.Store.TrySetActive("sample", "1.0.0", false, out var firstError));
        Assert.Null(firstError);
        Assert.True(fixture.Store.TrySetActive("sample", "1.0.0", false, out var secondError));
        Assert.Null(secondError);

        Assert.Equal(["sample@1.0.0"], fixture.Store.DisabledEntries);
        Assert.Equal(["sample@1.0.0"], ReadDisabledEntries(fixture.Store.SettingsPath));
    }

    private static IReadOnlyList<string> ReadDisabledEntries(string settingsPath)
    {
        var root = JsonNode.Parse(File.ReadAllText(settingsPath))!.AsObject();
        var entries = root["stringListSettings"]!["disabledMods"]!.AsArray();
        return entries.Select(node => node!.GetValue<string>()).ToArray();
    }

    private sealed class ClientSettingsFixture : IDisposable
    {
        private ClientSettingsFixture(string directoryPath, ClientSettingsStore store)
        {
            DirectoryPath = directoryPath;
            Store = store;
        }

        public string DirectoryPath { get; }

        public ClientSettingsStore Store { get; }

        public static ClientSettingsFixture Create(params string[] disabledEntries)
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), "SimpleVsManagerTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);

            var settingsPath = Path.Combine(directoryPath, "clientsettings.json");
            var root = new JsonObject
            {
                ["boolSettings"] = new JsonObject(),
                ["intSettings"] = new JsonObject(),
                ["floatSettings"] = new JsonObject(),
                ["stringSettings"] = new JsonObject(),
                ["stringListSettings"] = new JsonObject
                {
                    ["disabledMods"] = new JsonArray(
                        disabledEntries.Select(entry => JsonValue.Create(entry)).ToArray()),
                    ["modPaths"] = new JsonArray(
                        JsonValue.Create("Mods"),
                        JsonValue.Create(Path.Combine(directoryPath, "Mods")))
                }
            };

            File.WriteAllText(settingsPath, root.ToJsonString());
            return new ClientSettingsFixture(directoryPath, new ClientSettingsStore(directoryPath));
        }

        public FileStream LockSettingsFile()
        {
            return new FileStream(Store.SettingsPath, FileMode.Open, FileAccess.Read, FileShare.None);
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, true);
        }
    }
}
