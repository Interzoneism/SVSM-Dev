using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using SimpleVsManager.Cloud;
using VintageStoryModManager.Models;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class SubscriptionFeatureTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void RegistryParserSupportsRevisionsAndLegacyEntries()
    {
        using var document = JsonDocument.Parse("""
            {
              "new-entry": {
                "content": { "name": "New" },
                "dateAdded": "2026-08-10T10:00:00Z",
                "createdAt": "2026-08-09T10:00:00Z",
                "updatedAt": "2026-08-10T10:00:00Z",
                "revision": 4
              },
              "legacy-entry": {
                "content": { "name": "Legacy" },
                "dateAdded": "2025-01-02T03:04:05Z"
              }
            }
            """);
        var parser = typeof(FirebaseModlistStore).GetMethod(
            "ParseRegistryEntries",
            BindingFlags.NonPublic | BindingFlags.Static);

        var entries = Assert.IsType<List<CloudModlistRegistryEntry>>(
            parser!.Invoke(null, new object[] { document, false }));

        var current = Assert.Single(entries, entry => entry.RegistryId == "new-entry");
        Assert.Equal(4, current.Revision);
        Assert.Equal(DateTimeOffset.Parse("2026-08-09T10:00:00Z"), current.CreatedAt);
        Assert.Equal(DateTimeOffset.Parse("2026-08-10T10:00:00Z"), current.UpdatedAt);

        var legacy = Assert.Single(entries, entry => entry.RegistryId == "legacy-entry");
        Assert.Equal(1, legacy.Revision);
        Assert.Equal(legacy.DateAdded, legacy.CreatedAt);
        Assert.Equal(legacy.DateAdded, legacy.UpdatedAt);
    }

    [Fact]
    public void LocalSubscriptionStatusTracksAvailableRevision()
    {
        var subscription = new OnlineSubscriptionInfo
        {
            RegistryId = "registry-1",
            SyncedRevision = 2,
            LastKnownRemoteRevision = 3
        };
        var entry = new LocalModlistListEntry(
            "C:\\modlists\\example.json",
            "Example",
            null,
            "1.0",
            null,
            Array.Empty<string>(),
            null,
            null,
            onlineSubscription: subscription);

        Assert.True(entry.IsSubscribed);
        Assert.True(entry.HasUpdateAvailable);
        Assert.Equal("Update available", entry.SubscriptionStatusDisplay);
        Assert.Contains("local revision 2", entry.UpdateBadgeToolTip, StringComparison.Ordinal);
        Assert.Contains("online revision 3", entry.UpdateBadgeToolTip, StringComparison.Ordinal);
    }

    [Fact]
    public void SubscriptionMetadataRoundTripsThroughModlistJson()
    {
        var preset = new SerializablePreset
        {
            Name = "Subscribed",
            OnlineSubscription = new OnlineSubscriptionInfo
            {
                RegistryId = "registry-2",
                SyncedRevision = 7,
                LastKnownRemoteRevision = 9,
                SubscribedAtUtc = DateTimeOffset.Parse("2026-08-10T11:00:00Z")
            }
        };

        var json = JsonSerializer.Serialize(preset, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var restored = JsonSerializer.Deserialize<SerializablePreset>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(restored?.OnlineSubscription);
        Assert.Equal("registry-2", restored.OnlineSubscription.RegistryId);
        Assert.Equal(7, restored.OnlineSubscription.SyncedRevision);
        Assert.Equal(9, restored.OnlineSubscription.LastKnownRemoteRevision);
    }

    [Fact]
    public void MainWindowExposesSubscriptionCountsBadgesAndPersistentAlert()
    {
        var document = XDocument.Load(Path.Combine(
            FindRepositoryRoot(), "VintageStoryModManager", "Views", "MainWindow.xaml"));

        Assert.NotNull(document.Descendants(Presentation + "TextBlock").Single(element =>
            element.Attribute(Xaml + "Name")?.Value == "SubscribedModlistUpdatesTextBlock"));
        Assert.Equal(2, document.Descendants(Presentation + "TextBlock").Count(element =>
            element.Attribute("Text")?.Value == "↻"
            && element.Attribute("ToolTip")?.Value == "{Binding UpdateBadgeToolTip}"));
        Assert.Contains(document.Descendants(Presentation + "DataGridTextColumn"), column =>
            column.Attribute("Header")?.Value == "Subscribers"
            && column.Attribute("Binding")?.Value == "{Binding SubscriberCount}");
        Assert.Contains(document.Descendants(Presentation + "DataGridTextColumn"), column =>
            column.Attribute("Header")?.Value == "Subscription"
            && column.Attribute("Binding")?.Value == "{Binding SubscriptionStatusDisplay}");
    }

    [Fact]
    public void FirebaseRulesProtectMembershipsAndOwnerNotices()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(FindRepositoryRoot(),
            "firebase.rules.json")));
        var rules = document.RootElement.GetProperty("rules");
        var subscriptionWrite = rules.GetProperty("modlistSubscriptions")
            .GetProperty("$entryId").GetProperty("$subscriberId").GetProperty(".write").GetString();
        var noticeRead = rules.GetProperty("modlistSubscriptionNotices")
            .GetProperty("$entryId").GetProperty(".read").GetString();

        Assert.Contains("$subscriberId == auth.uid", subscriptionWrite, StringComparison.Ordinal);
        Assert.Contains("registryOwners", subscriptionWrite, StringComparison.Ordinal);
        Assert.Contains("registryOwners", noticeRead, StringComparison.Ordinal);
        Assert.Contains("auth.uid", noticeRead, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        foreach (var candidate in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(candidate);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "ImprovedModMenu.sln")))
                    return directory.FullName;
                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
