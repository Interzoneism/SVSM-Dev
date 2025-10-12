using System;

namespace VintageStoryModManager.Models;

/// <summary>
/// Represents a cloud modlist entry prepared for display in the UI.
/// </summary>
public sealed class CloudModlistListEntry
{
    public CloudModlistListEntry(string ownerId, string slotKey, string? name, string slotLabel, string contentJson)
    {
        OwnerId = ownerId ?? throw new ArgumentNullException(nameof(ownerId));
        SlotKey = slotKey ?? throw new ArgumentNullException(nameof(slotKey));
        Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        SlotLabel = slotLabel ?? throw new ArgumentNullException(nameof(slotLabel));
        ContentJson = contentJson ?? throw new ArgumentNullException(nameof(contentJson));
    }

    public string OwnerId { get; }

    public string SlotKey { get; }

    public string? Name { get; }

    public string SlotLabel { get; }

    public string ContentJson { get; }

    public string DisplayName => Name ?? $"{OwnerId} ({SlotLabel})";
}
