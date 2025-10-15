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
        private readonly FirebaseAnonymousAuthenticator _authenticator;

        private string? _playerUid;
        private string? _playerName;

        public FirebaseModlistStore(string dbUrl, FirebaseAnonymousAuthenticator authenticator)
        {
            _dbUrl = (dbUrl ?? throw new ArgumentNullException(nameof(dbUrl))).TrimEnd('/');
            _authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
        }

        internal FirebaseAnonymousAuthenticator Authenticator => _authenticator;

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

            string normalizedContent = ReplaceUploader(document.RootElement, identity);
            string nodeJson = BuildNodeJson(normalizedContent);

            using (HttpResponseMessage response = await SendWithAuthRetryAsync(token =>
                   {
                       string userUrl = BuildAuthenticatedUrl(token, null, "users", identity.Uid, slotKey);
                       var request = new HttpRequestMessage(HttpMethod.Put, userUrl)
                       {
                           Content = new StringContent(nodeJson, Encoding.UTF8, "application/json")
                       };
                       return HttpClient.SendAsync(request, ct);
                   }, ct).ConfigureAwait(false))
            {
                await EnsureOk(response, "Save").ConfigureAwait(false);
            }

            await EnsureUploaderConsistencyAsync(identity, ct).ConfigureAwait(false);
        }

        /// <summary>Load a JSON string from the slot. Returns null if missing.</summary>
        public async Task<string?> LoadAsync(string slotKey, CancellationToken ct = default)
        {
            ValidateSlotKey(slotKey);
            var identity = GetIdentityComponents();

            using HttpResponseMessage response = await SendWithAuthRetryAsync(token =>
                {
                    string userUrl = BuildAuthenticatedUrl(token, null, "users", identity.Uid, slotKey);
                    return HttpClient.GetAsync(userUrl, ct);
                }, ct).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            await EnsureOk(response, "Load").ConfigureAwait(false);

            byte[] bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
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

            using HttpResponseMessage response = await SendWithAuthRetryAsync(token =>
                {
                    string userUrl = BuildAuthenticatedUrl(token, null, "users", identity.Uid, slotKey);
                    return HttpClient.DeleteAsync(userUrl, ct);
                }, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
            {
                await EnsureOk(response, "Delete").ConfigureAwait(false);
            }
        }

        public async Task<IReadOnlyList<CloudModlistRegistryEntry>> GetRegistryEntriesAsync(CancellationToken ct = default)
        {
            using HttpResponseMessage response = await SendWithAuthRetryAsync(token =>
                {
                    string registryUrl = BuildAuthenticatedUrl(token, null, "registry");
                    return HttpClient.GetAsync(registryUrl, ct);
                }, ct).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return Array.Empty<CloudModlistRegistryEntry>();
            }

            await EnsureOk(response, "Fetch registry").ConfigureAwait(false);

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

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
        private async Task<IReadOnlyList<string>> ListSlotsAsync((string Uid, string Name) identity, CancellationToken ct)
        {
            using HttpResponseMessage response = await SendWithAuthRetryAsync(token =>
                {
                    string url = BuildAuthenticatedUrl(token, "shallow=true", "users", identity.Uid);
                    return HttpClient.GetAsync(url, ct);
                }, ct).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return Array.Empty<string>();
            }

            await EnsureOk(response, "List").ConfigureAwait(false);

            string text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
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

        private async Task EnsureUploaderConsistencyAsync((string Uid, string Name) identity, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(identity.Name))
            {
                return;
            }

            IReadOnlyList<string> slots = await ListSlotsAsync(identity, ct).ConfigureAwait(false);
            foreach (string slot in slots)
            {
                await UpdateSlotUploaderAsync(slot, identity, ct).ConfigureAwait(false);
            }
        }

        private async Task UpdateSlotUploaderAsync(string slotKey, (string Uid, string Name) identity, CancellationToken ct)
        {
            using HttpResponseMessage response = await SendWithAuthRetryAsync(token =>
                {
                    string userUrl = BuildAuthenticatedUrl(token, null, "users", identity.Uid, slotKey);
                    return HttpClient.GetAsync(userUrl, ct);
                }, ct).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return;
            }

            await EnsureOk(response, "Fetch modlist for uploader sync").ConfigureAwait(false);

            byte[] bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
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
            if (string.Equals(existingUploader, identity.Name, StringComparison.Ordinal))
            {
                return;
            }

            string updatedContentJson = ReplaceUploader(node.Value.Content, identity);
            string updatedNodeJson = BuildNodeJson(updatedContentJson);

            using HttpResponseMessage putResponse = await SendWithAuthRetryAsync(token =>
                {
                    string userUrl = BuildAuthenticatedUrl(token, null, "users", identity.Uid, slotKey);
                    var request = new HttpRequestMessage(HttpMethod.Put, userUrl)
                    {
                        Content = new StringContent(updatedNodeJson, Encoding.UTF8, "application/json")
                    };
                    return HttpClient.SendAsync(request, ct);
                }, ct).ConfigureAwait(false);

            await EnsureOk(putResponse, "Update modlist uploader").ConfigureAwait(false);
        }

        private (string Uid, string Name) GetIdentityComponents()
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

            return (uid, name);
        }

        private static async Task EnsureOk(HttpResponseMessage response, string operation)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                string message = $"{operation} failed: access was denied by the Firebase security rules. Ensure anonymous authentication is enabled and that the configured Firebase API key has access to the database.";
                LogRestFailure(message);
                throw new InvalidOperationException(message);
            }

            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
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

            if (root.TryGetProperty("uploader", out JsonElement uploaderProperty) && uploaderProperty.ValueKind == JsonValueKind.String)
            {
                string? uploader = uploaderProperty.GetString();
                if (!string.IsNullOrWhiteSpace(uploader))
                {
                    return uploader.Trim();
                }
            }

            if (root.TryGetProperty("uploaderName", out JsonElement uploaderNameProperty) && uploaderNameProperty.ValueKind == JsonValueKind.String)
            {
                string? uploaderName = uploaderNameProperty.GetString();
                return string.IsNullOrWhiteSpace(uploaderName) ? null : uploaderName.Trim();
            }

            return null;
        }

        private async Task<HttpResponseMessage> SendWithAuthRetryAsync(Func<string, Task<HttpResponseMessage>> operation, CancellationToken ct)
        {
            bool hasRetried = false;

            while (true)
            {
                string token = await _authenticator.GetIdTokenAsync(ct).ConfigureAwait(false);
                HttpResponseMessage response = await operation(token).ConfigureAwait(false);

                if (IsAuthError(response.StatusCode) && !hasRetried)
                {
                    hasRetried = true;
                    response.Dispose();
                    await _authenticator.MarkTokenAsExpiredAsync(ct).ConfigureAwait(false);
                    continue;
                }

                return response;
            }
        }

        private string BuildAuthenticatedUrl(string authToken, string? query, params string[] segments)
        {
            var builder = new StringBuilder(_dbUrl.Length + 64);
            builder.Append(_dbUrl);
            builder.Append('/');

            for (int i = 0; i < segments.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append('/');
                }

                builder.Append(Uri.EscapeDataString(segments[i]));
            }

            builder.Append(".json?auth=");
            builder.Append(Uri.EscapeDataString(authToken));

            if (!string.IsNullOrWhiteSpace(query))
            {
                builder.Append('&');
                builder.Append(query);
            }

            return builder.ToString();
        }

        private static bool IsAuthError(HttpStatusCode statusCode)
            => statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;

        private static string ReplaceUploader(JsonElement root, (string Uid, string Name) identity)
        {
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                bool hasUploader = false;
                bool hasUploaderName = false;
                bool hasUploaderId = false;

                foreach (JsonProperty property in root.EnumerateObject())
                {
                    if (property.NameEquals("uploader"))
                    {
                        writer.WriteString("uploader", identity.Name);
                        hasUploader = true;
                    }
                    else if (property.NameEquals("uploaderName"))
                    {
                        writer.WriteString("uploaderName", identity.Name);
                        hasUploaderName = true;
                    }
                    else if (property.NameEquals("uploaderId"))
                    {
                        writer.WriteString("uploaderId", identity.Uid);
                        hasUploaderId = true;
                    }
                    else
                    {
                        property.WriteTo(writer);
                    }
                }

                if (!hasUploader)
                {
                    writer.WriteString("uploader", identity.Name);
                }

                if (!hasUploaderName)
                {
                    writer.WriteString("uploaderName", identity.Name);
                }

                if (!hasUploaderId)
                {
                    writer.WriteString("uploaderId", identity.Uid);
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
