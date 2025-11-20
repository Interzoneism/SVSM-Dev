using System.Text.Json;
using System.Linq;

namespace VintageStoryModManager.Models;

/// <summary>
///     Minimal metadata required to display a cloud modlist in the UI.
/// </summary>
public sealed class CloudModlistSummary
{
    public static readonly CloudModlistSummary Empty = new(null, null, null, null, Array.Empty<ModReference>(), null);

    public CloudModlistSummary(
        string? name,
        string? description,
        string? version,
        string? uploader,
        IReadOnlyList<ModReference> mods,
        string? gameVersion)
    {
        Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Version = string.IsNullOrWhiteSpace(version) ? null : version.Trim();
        Uploader = string.IsNullOrWhiteSpace(uploader) ? null : uploader.Trim();
        Mods = mods ?? Array.Empty<ModReference>();
        GameVersion = string.IsNullOrWhiteSpace(gameVersion) ? null : gameVersion.Trim();
    }

    public string? Name { get; }

    public string? Description { get; }

    public string? Version { get; }

    public string? Uploader { get; }

    public IReadOnlyList<ModReference> Mods { get; }

    public string? GameVersion { get; }

    public static CloudModlistSummary FromJsonElement(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return Empty;

        var name = TryGetTrimmedProperty(element, "name");
        var description = TryGetTrimmedProperty(element, "description");
        var version = TryGetTrimmedProperty(element, "version");
        var uploader = TryGetTrimmedProperty(element, "uploader")
                       ?? TryGetTrimmedProperty(element, "uploaderName");
        var gameVersion = TryGetTrimmedProperty(element, "gameVersion")
                          ?? TryGetTrimmedProperty(element, "vsVersion");

        var mods = new List<ModReference>();
        if (element.TryGetProperty("mods", out var modsElement) && modsElement.ValueKind == JsonValueKind.Array)
            foreach (var mod in modsElement.EnumerateArray())
            {
                if (mod.ValueKind != JsonValueKind.Object) continue;

                var modId = TryGetTrimmedProperty(mod, "modId");
                if (string.IsNullOrWhiteSpace(modId)) continue;

                var modVersion = TryGetTrimmedProperty(mod, "version");
                mods.Add(new ModReference(modId, modVersion));
            }

        return new CloudModlistSummary(name, description, version, uploader, mods, gameVersion);
    }

    public object ToFirebasePayload(string dateAddedIso)
    {
        return new
        {
            content = new
            {
                name = Name,
                description = Description,
                version = Version,
                uploader = Uploader,
                uploaderName = Uploader,
                gameVersion = GameVersion,
                vsVersion = GameVersion,
                mods = Mods.Select(m => new { modId = m.ModId, version = m.Version })
            },
            dateAdded = dateAddedIso
        };
    }

    private static string? TryGetTrimmedProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property))
            if (property.ValueKind == JsonValueKind.String)
            {
                var value = property.GetString();
                return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            }

        return null;
    }

    public readonly struct ModReference
    {
        public ModReference(string modId, string? version)
        {
            ModId = modId ?? throw new ArgumentNullException(nameof(modId));
            Version = string.IsNullOrWhiteSpace(version) ? null : version.Trim();
        }

        public string ModId { get; }

        public string? Version { get; }
    }
}
