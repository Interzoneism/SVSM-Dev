using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;

namespace SimpleVsManager.Cloud
{
    /// <summary>
    /// Firebase RTDB access that relies solely on the player's Vintage Story identity.
    /// </summary>
    public sealed class FirebaseModlistStore
    {
        private static readonly HttpClient HttpClient = new();

        private readonly string _dbUrl;

        private string? _playerUid;
        private string? _playerName;

        public FirebaseModlistStore(string dbUrl)
        {
            _dbUrl = (dbUrl ?? throw new ArgumentNullException(nameof(dbUrl))).TrimEnd('/');
        }

        /// <summary>
        /// Gets the known slot keys used for storing modlists in Firebase.
        /// </summary>
        public static IReadOnlyList<string> SlotKeys => KnownSlots;

        /// <summary>
        /// Gets the identifier used for Firebase storage operations (player UID).
        /// </summary>
        public string? CurrentUserId => string.IsNullOrWhiteSpace(_playerUid) ? null : _playerUid;

        /// <summary>
        /// Applies the current player identity sourced from clientsettings.json.
        /// </summary>
        public void SetPlayerIdentity(string? playerUid, string? playerName)
        {
            _playerUid = Normalize(playerUid);
            _playerName = Normalize(playerName);
        }

        /// <summary>Save or replace the JSON in the given slot (e.g., "slot1").</summary>
        public async Task SaveAsync(string slotKey, string modlistJson, CancellationToken ct = default)
        {
            ValidateSlotKey(slotKey);
            var identity = GetIdentityComponents();

            using var document = JsonDocument.Parse(modlistJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("The modlist JSON must be an object.");
            }

            string normalizedContent = ReplaceUploader(document.RootElement, identity.Name);
            string nodeJson = BuildNodeJson(normalizedContent);

            string userUrl = BuildUserSlotUrl(identity.Uid, slotKey, identity.Query);
            using (var content = new StringContent(nodeJson, Encoding.UTF8, "application/json"))
            using (var response = await HttpClient.PutAsync(userUrl, content, ct))
            {
                await EnsureOk(response, "Save");
            }

            await SaveRegistryEntryAsync(slotKey, nodeJson, identity, ct);
            await EnsureUploaderConsistencyAsync(identity.Name, identity, ct);
        }

        /// <summary>Load a JSON string from the slot. Returns null if missing.</summary>
        public async Task<string?> LoadAsync(string slotKey, CancellationToken ct = default)
        {
            ValidateSlotKey(slotKey);
            var identity = GetIdentityComponents();

            string userUrl = BuildUserSlotUrl(identity.Uid, slotKey, identity.Query);
            using var response = await HttpClient.GetAsync(userUrl, ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            await EnsureOk(response, "Load");

            byte[] bytes = await response.Content.ReadAsByteArrayAsync(ct);
            if (bytes.Length == 0)
            {
                return null;
            }

            ModlistNode? node = JsonSerializer.Deserialize<ModlistNode?>(bytes, JsonOpts);
            if (node is null || node.Value.Content.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return null;
            }

            return node.Value.Content.GetRawText();
        }

        /// <summary>Return all present slot keys (subset of slot1..slot5).</summary>
        public async Task<IReadOnlyList<string>> ListSlotsAsync(CancellationToken ct = default)
        {
            var identity = GetIdentityComponents();
            return await ListSlotsAsync(identity, ct);
        }

        /// <summary>Delete a slot if present (idempotent).</summary>
        public async Task DeleteAsync(string slotKey, CancellationToken ct = default)
        {
            ValidateSlotKey(slotKey);
            var identity = GetIdentityComponents();

            string userUrl = BuildUserSlotUrl(identity.Uid, slotKey, identity.Query);
            using var response = await HttpClient.DeleteAsync(userUrl, ct);

            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
            {
                await EnsureOk(response, "Delete");
            }

            await DeleteRegistryEntryAsync(slotKey, identity, ct);
        }

        public async Task<IReadOnlyList<CloudModlistRegistryEntry>> GetRegistryEntriesAsync(CancellationToken ct = default)
        {
            string? identityQuery = TryBuildIdentityQuery();
            string registryUrl = string.IsNullOrWhiteSpace(identityQuery)
                ? $"{_dbUrl}/registry.json"
                : $"{_dbUrl}/registry.json?{identityQuery}";

            using var response = await HttpClient.GetAsync(registryUrl, ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return Array.Empty<CloudModlistRegistryEntry>();
            }

            await EnsureOk(response, "Fetch registry");

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<CloudModlistRegistryEntry>();
            }

            var results = new List<CloudModlistRegistryEntry>();

            foreach (JsonProperty userProperty in document.RootElement.EnumerateObject())
            {
                string ownerId = userProperty.Name;
                if (string.IsNullOrWhiteSpace(ownerId) || userProperty.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (JsonProperty slotProperty in userProperty.Value.EnumerateObject())
                {
                    string slotKey = slotProperty.Name;
                    if (!IsKnownSlot(slotKey) || slotProperty.Value.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (!slotProperty.Value.TryGetProperty("content", out JsonElement contentElement))
                    {
                        continue;
                    }

                    if (contentElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                    {
                        continue;
                    }

                    string json = contentElement.GetRawText();
                    results.Add(new CloudModlistRegistryEntry(ownerId, slotKey, json));
                }
            }

            return results;
        }

        /// <summary>Return the first available slot key (slot1..slot5), or null if full.</summary>
        public async Task<string?> GetFirstFreeSlotAsync(CancellationToken ct = default)
        {
            IReadOnlyList<string> existing = await ListSlotsAsync(ct);
            foreach (string slot in KnownSlots)
            {
                if (!existing.Contains(slot))
                {
                    return slot;
                }
            }

            return null;
        }
        private async Task<IReadOnlyList<string>> ListSlotsAsync((string Uid, string Name, string Query) identity, CancellationToken ct)
        {
            string url = $"{_dbUrl}/users/{Uri.EscapeDataString(identity.Uid)}/lists.json?shallow=true&{identity.Query}";
            using var response = await HttpClient.GetAsync(url, ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return Array.Empty<string>();
            }

            await EnsureOk(response, "List");

            string text = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(text) || text == "null")
            {
                return Array.Empty<string>();
            }

            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(text, JsonOpts);
            if (dict is null)
            {
                return Array.Empty<string>();
            }

            var list = new List<string>(KnownSlots.Length);
            foreach (string slot in KnownSlots)
            {
                if (dict.ContainsKey(slot))
                {
                    list.Add(slot);
                }
            }

            return list;
        }

        private async Task EnsureUploaderConsistencyAsync(string uploader, (string Uid, string Name, string Query) identity, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(uploader))
            {
                return;
            }

            IReadOnlyList<string> slots = await ListSlotsAsync(identity, ct);
            foreach (string slot in slots)
            {
                await UpdateSlotUploaderAsync(slot, uploader, identity, ct);
            }
        }

        private async Task UpdateSlotUploaderAsync(string slotKey, string uploader, (string Uid, string Name, string Query) identity, CancellationToken ct)
        {
            string userUrl = BuildUserSlotUrl(identity.Uid, slotKey, identity.Query);
            using var response = await HttpClient.GetAsync(userUrl, ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return;
            }

            await EnsureOk(response, "Fetch modlist for uploader sync");

            byte[] bytes = await response.Content.ReadAsByteArrayAsync(ct);
            if (bytes.Length == 0)
            {
                return;
            }

            ModlistNode? node = JsonSerializer.Deserialize<ModlistNode?>(bytes, JsonOpts);
            if (node is null || node.Value.Content.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            string? existingUploader = TryGetUploader(node.Value.Content);
            if (string.Equals(existingUploader, uploader, StringComparison.Ordinal))
            {
                return;
            }

            string updatedContentJson = ReplaceUploader(node.Value.Content, uploader);
            string updatedNodeJson = BuildNodeJson(updatedContentJson);

            using (var content = new StringContent(updatedNodeJson, Encoding.UTF8, "application/json"))
            using (var putResponse = await HttpClient.PutAsync(userUrl, content, ct))
            {
                await EnsureOk(putResponse, "Update modlist uploader");
            }

            await SaveRegistryEntryAsync(slotKey, updatedNodeJson, identity, ct);
        }

        private async Task SaveRegistryEntryAsync(string slotKey, string nodeJson, (string Uid, string Name, string Query) identity, CancellationToken ct)
        {
            string url = $"{_dbUrl}/registry/{Uri.EscapeDataString(identity.Uid)}/{Uri.EscapeDataString(slotKey)}.json?{identity.Query}";
            using var content = new StringContent(nodeJson, Encoding.UTF8, "application/json");
            using var response = await HttpClient.PutAsync(url, content, ct);
            await EnsureOk(response, "Save registry entry");
        }

        private async Task DeleteRegistryEntryAsync(string slotKey, (string Uid, string Name, string Query) identity, CancellationToken ct)
        {
            string url = $"{_dbUrl}/registry/{Uri.EscapeDataString(identity.Uid)}/{Uri.EscapeDataString(slotKey)}.json?{identity.Query}";
            using var response = await HttpClient.DeleteAsync(url, ct);

            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
            {
                return;
            }

            await EnsureOk(response, "Delete registry entry");
        }

        private (string Uid, string Name, string Query) GetIdentityComponents()
        {
            string? uid = _playerUid;
            string? name = _playerName;

            if (string.IsNullOrWhiteSpace(uid))
            {
                throw new InvalidOperationException("The Vintage Story clientsettings.json file does not contain a playeruid value. Start the game once to generate it.");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("The Vintage Story clientsettings.json file does not contain a playername value. Set a player name in the game before using cloud modlists.");
            }

            uid = uid.Trim();
            name = name.Trim();

            string query = $"playerUid={Uri.EscapeDataString(uid)}&playerName={Uri.EscapeDataString(name)}";
            return (uid, name, query);
        }

        private string? TryBuildIdentityQuery()
        {
            if (string.IsNullOrWhiteSpace(_playerUid) || string.IsNullOrWhiteSpace(_playerName))
            {
                return null;
            }

            return $"playerUid={Uri.EscapeDataString(_playerUid.Trim())}&playerName={Uri.EscapeDataString(_playerName.Trim())}";
        }

        private string BuildUserSlotUrl(string uid, string slotKey, string query)
        {
            return $"{_dbUrl}/users/{Uri.EscapeDataString(uid)}/lists/{Uri.EscapeDataString(slotKey)}.json?{query}";
        }

        private static async Task EnsureOk(HttpResponseMessage response, string operation)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                string message = $"{operation} failed: access was denied by the Firebase security rules. Ensure the database allows requests authenticated with playerUid/playerName query parameters.";
                LogRestFailure(message);
                throw new InvalidOperationException(message);
            }

            string body = await response.Content.ReadAsStringAsync();
            LogRestFailure($"{operation} failed: {(int)response.StatusCode} {response.ReasonPhrase} | {Truncate(body, 400)}");
            throw new InvalidOperationException($"{operation} failed: {(int)response.StatusCode} {response.ReasonPhrase} | {Truncate(body, 400)}");
        }

        private static void LogRestFailure(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            StatusLogService.AppendStatus(message, true);
        }

        private static string? TryGetUploader(JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!root.TryGetProperty("uploader", out JsonElement uploaderProperty) || uploaderProperty.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            string? uploader = uploaderProperty.GetString();
            return string.IsNullOrWhiteSpace(uploader) ? null : uploader.Trim();
        }

        private static string ReplaceUploader(JsonElement root, string uploader)
        {
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                bool hasUploader = false;

                foreach (JsonProperty property in root.EnumerateObject())
                {
                    if (property.NameEquals("uploader"))
                    {
                        writer.WriteString("uploader", uploader);
                        hasUploader = true;
                    }
                    else
                    {
                        property.WriteTo(writer);
                    }
                }

                if (!hasUploader)
                {
                    writer.WriteString("uploader", uploader);
                }

                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(buffer.WrittenSpan);
        }

        private static string BuildNodeJson(string contentJson)
        {
            using var doc = JsonDocument.Parse(contentJson);
            var node = new ModlistNode { Content = doc.RootElement.Clone() };
            return JsonSerializer.Serialize(node, JsonOpts);
        }

        private static void ValidateSlotKey(string slotKey)
        {
            if (Array.IndexOf(KnownSlots, slotKey) < 0)
            {
                throw new ArgumentException("Slot must be one of: slot1, slot2, slot3, slot4, slot5.", nameof(slotKey));
            }
        }

        private static string? Normalize(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string Truncate(string value, int max)
            => value.Length <= max ? value : value.Substring(0, max) + "...";

        private static readonly string[] KnownSlots = { "slot1", "slot2", "slot3", "slot4", "slot5" };

        private static bool IsKnownSlot(string slotKey)
            => Array.IndexOf(KnownSlots, slotKey) >= 0;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private struct ModlistNode
        {
            [JsonPropertyName("content")]
            public JsonElement Content { get; set; }
        }
    }
}
