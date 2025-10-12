using System;
using System.Collections.Generic;
using System.IO;
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
    /// Firebase RTDB + Anonymous Auth (REST) for 5 fixed slots: slot1..slot5.
    /// Stores raw modlist JSON under { "content": <raw> }.
    /// Persists uid/refresh/idToken locally and auto-refreshes tokens.
    /// </summary>
    public sealed class FirebaseModlistStore
    {
        private static readonly HttpClient _http = new HttpClient();

        private readonly string _apiKey;   // e.g., "AIzaSy..."
        private readonly string _dbUrl;    // e.g., "https://<project>.firebaseio.com"
        private readonly string _persistPath;

        private AuthState _auth = new();

        public FirebaseModlistStore(string apiKey, string dbUrl, string appDataDir)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _dbUrl = (dbUrl ?? throw new ArgumentNullException(nameof(dbUrl))).TrimEnd('/');
            Directory.CreateDirectory(appDataDir);
            _persistPath = Path.Combine(appDataDir, "firebase_auth.json");

            LoadAuthStateFromDisk();
        }

        /// <summary>
        /// Gets the known slot keys used for storing modlists in Firebase.
        /// </summary>
        public static IReadOnlyList<string> SlotKeys => KnownSlots;

        /// <summary>
        /// Gets the identifier assigned to the currently authenticated Firebase user.
        /// </summary>
        public string? CurrentUserId => _auth.Uid;

        // -------------------------
        // Public API
        // -------------------------

        public async Task EnsureSignedInAsync(CancellationToken ct = default)
        {
            // If we have a valid idToken, ensure not expired (with small skew).
            if (!string.IsNullOrWhiteSpace(_auth.IdToken) &&
                _auth.IdTokenExpiryUtc.HasValue &&
                DateTimeOffset.UtcNow < _auth.IdTokenExpiryUtc.Value.AddSeconds(-60))
            {
                return; // still valid
            }

            // If we can refresh, prefer that (keeps the same uid).
            if (!string.IsNullOrWhiteSpace(_auth.RefreshToken))
            {
                var ok = await TryRefreshAsync(ct);
                if (ok) return;
                // Fall through to sign-up if refresh fails
            }

            await SignUpAnonymousAsync(ct);
        }

        /// <summary>Save or replace the JSON in the given slot (e.g., "slot1").</summary>
        public async Task SaveAsync(string slotKey, string modlistJson, CancellationToken ct = default)
        {
            ValidateSlotKey(slotKey);
            await EnsureSignedInAsync(ct);

            using var doc = JsonDocument.Parse(modlistJson);
            var node = new ModlistNode { Content = doc.RootElement.Clone() };

            var json = JsonSerializer.Serialize(node, JsonOpts);
            var userUrl = $"{_dbUrl}/users/{_auth.Uid}/lists/{Uri.EscapeDataString(slotKey)}.json?auth={_auth.IdToken}";

            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            using (var resp = await _http.PutAsync(userUrl, content, ct))
            {
                await EnsureOk(resp, "Save");
            }

            await SaveRegistryEntryAsync(slotKey, json, ct);
        }

        /// <summary>Load a JSON string from the slot. Returns null if missing.</summary>
        public async Task<string?> LoadAsync(string slotKey, CancellationToken ct = default)
        {
            ValidateSlotKey(slotKey);
            await EnsureSignedInAsync(ct);

            var url = $"{_dbUrl}/users/{_auth.Uid}/lists/{Uri.EscapeDataString(slotKey)}.json?auth={_auth.IdToken}";
            using var resp = await _http.GetAsync(url, ct);

            if (resp.StatusCode == HttpStatusCode.NotFound) return null;
            await EnsureOk(resp, "Load");

            var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
            if (bytes.Length == 0) return null;

            var node = JsonSerializer.Deserialize<ModlistNode?>(bytes, JsonOpts);
            if (node is null || node.Value.Content.ValueKind == JsonValueKind.Undefined || node.Value.Content.ValueKind == JsonValueKind.Null)
                return null;

            return node.Value.Content.GetRawText();
        }

        /// <summary>Return all present slot keys (subset of slot1..slot5).</summary>
        public async Task<IReadOnlyList<string>> ListSlotsAsync(CancellationToken ct = default)
        {
            await EnsureSignedInAsync(ct);

            var url = $"{_dbUrl}/users/{_auth.Uid}/lists.json?shallow=true&auth={_auth.IdToken}";
            using var resp = await _http.GetAsync(url, ct);

            if (resp.StatusCode == HttpStatusCode.NotFound) return Array.Empty<string>();
            await EnsureOk(resp, "List");

            var text = await resp.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(text) || text == "null") return Array.Empty<string>();

            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(text, JsonOpts);
            if (dict is null) return Array.Empty<string>();

            // Only surface known slots, ignore any unknown keys
            var list = new List<string>(5);
            foreach (var k in KnownSlots)
                if (dict.ContainsKey(k)) list.Add(k);
            return list;
        }

        /// <summary>Delete a slot if present (idempotent).</summary>
        public async Task DeleteAsync(string slotKey, CancellationToken ct = default)
        {
            ValidateSlotKey(slotKey);
            await EnsureSignedInAsync(ct);

            var userUrl = $"{_dbUrl}/users/{_auth.Uid}/lists/{Uri.EscapeDataString(slotKey)}.json?auth={_auth.IdToken}";
            using var resp = await _http.DeleteAsync(userUrl, ct);
            // 200/204/404 are all "fine" for our UX.
            if (!resp.IsSuccessStatusCode && resp.StatusCode != HttpStatusCode.NotFound)
            {
                await EnsureOk(resp, "Delete");
            }

            await DeleteRegistryEntryAsync(slotKey, ct);
        }

        public async Task<IReadOnlyList<CloudModlistRegistryEntry>> GetRegistryEntriesAsync(CancellationToken ct = default)
        {
            await EnsureSignedInAsync(ct);

            string url = $"{_dbUrl}/registry.json?auth={_auth.IdToken}";
            using var resp = await _http.GetAsync(url, ct);

            if (resp.StatusCode == HttpStatusCode.NotFound)
            {
                return Array.Empty<CloudModlistRegistryEntry>();
            }

            await EnsureOk(resp, "Fetch registry");

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<CloudModlistRegistryEntry>();
            }

            var results = new List<CloudModlistRegistryEntry>();

            foreach (JsonProperty userProperty in doc.RootElement.EnumerateObject())
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

                    if (contentElement.ValueKind == JsonValueKind.Null || contentElement.ValueKind == JsonValueKind.Undefined)
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
            var existing = await ListSlotsAsync(ct);
            foreach (var s in KnownSlots)
                if (!existing.Contains(s)) return s;
            return null;
        }

        // -------------------------
        // Internals
        // -------------------------

        private async Task SignUpAnonymousAsync(CancellationToken ct)
        {
            var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={_apiKey}";
            var payload = new { returnSecureToken = true };
            var json = JsonSerializer.Serialize(payload, JsonOpts);

            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync(url, content, ct);
            await EnsureOk(resp, "SignUp");

            var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
            var data = JsonSerializer.Deserialize<AnonAuthResponse>(bytes, JsonOpts)
                       ?? throw new InvalidOperationException("Auth parse failed.");

            if (string.IsNullOrWhiteSpace(data.idToken) || string.IsNullOrWhiteSpace(data.localId))
                throw new InvalidOperationException("Missing idToken or uid.");

            var expires = SecondsFromString(data.expiresIn);

            _auth = new AuthState
            {
                Uid = data.localId!,
                IdToken = data.idToken!,
                RefreshToken = data.refreshToken,
                IdTokenExpiryUtc = DateTimeOffset.UtcNow.AddSeconds(expires > 60 ? expires : 3600)
            };

            SaveAuthStateToDisk();
        }

        private async Task<bool> TryRefreshAsync(CancellationToken ct)
        {
            try
            {
                var url = $"https://securetoken.googleapis.com/v1/token?key={_apiKey}";
                // x-www-form-urlencoded
                var form = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = _auth.RefreshToken!
                });

                using var resp = await _http.PostAsync(url, form, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync(ct);
                    LogRestFailure($"Token refresh failed: {(int)resp.StatusCode} {resp.ReasonPhrase} | {Truncate(body, 400)}");
                    return false;
                }

                var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
                var data = JsonSerializer.Deserialize<RefreshResponse>(bytes, JsonOpts);
                if (data is null || string.IsNullOrWhiteSpace(data.id_token)) return false;

                var expires = SecondsFromString(data.expires_in);

                _auth.IdToken = data.id_token!;
                _auth.IdTokenExpiryUtc = DateTimeOffset.UtcNow.AddSeconds(expires > 60 ? expires : 3600);

                // Some responses also return user_id and new refresh_token; keep existing if absent
                if (!string.IsNullOrWhiteSpace(data.refresh_token))
                    _auth.RefreshToken = data.refresh_token;

                SaveAuthStateToDisk();
                return true;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                LogRestFailure($"Token refresh exception: {ex.Message}");
                return false;
            }
        }

        private static async Task EnsureOk(HttpResponseMessage resp, string opName)
        {
            if (resp.IsSuccessStatusCode) return;

            var body = await resp.Content.ReadAsStringAsync();
            LogRestFailure($"{opName} failed: {(int)resp.StatusCode} {resp.ReasonPhrase} | {Truncate(body, 400)}");
            throw new InvalidOperationException($"{opName} failed: {(int)resp.StatusCode} {resp.ReasonPhrase} | {Truncate(body, 400)}");
        }

        private async Task SaveRegistryEntryAsync(string slotKey, string nodeJson, CancellationToken ct)
        {
            var url = $"{_dbUrl}/registry/{_auth.Uid}/{Uri.EscapeDataString(slotKey)}.json?auth={_auth.IdToken}";

            using var content = new StringContent(nodeJson, Encoding.UTF8, "application/json");
            using var resp = await _http.PutAsync(url, content, ct);
            await EnsureOk(resp, "Save registry entry");
        }

        private async Task DeleteRegistryEntryAsync(string slotKey, CancellationToken ct)
        {
            var url = $"{_dbUrl}/registry/{_auth.Uid}/{Uri.EscapeDataString(slotKey)}.json?auth={_auth.IdToken}";
            using var resp = await _http.DeleteAsync(url, ct);

            if (resp.IsSuccessStatusCode || resp.StatusCode == HttpStatusCode.NotFound)
            {
                return;
            }

            await EnsureOk(resp, "Delete registry entry");
        }

        private static bool IsKnownSlot(string slotKey)
        {
            foreach (string known in KnownSlots)
            {
                if (string.Equals(known, slotKey, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void LogRestFailure(string message)
        {
            StatusLogService.AppendStatus(message, true);
        }

        private void LoadAuthStateFromDisk()
        {
            try
            {
                if (!File.Exists(_persistPath)) return;
                var json = File.ReadAllText(_persistPath, Encoding.UTF8);
                var state = JsonSerializer.Deserialize<AuthState>(json, JsonOpts);
                if (state != null) _auth = state;
            }
            catch { /* ignore */ }
        }

        private void SaveAuthStateToDisk()
        {
            try
            {
                var json = JsonSerializer.Serialize(_auth, JsonOptsIndented);
                File.WriteAllText(_persistPath, json, Encoding.UTF8);
            }
            catch { /* ignore */ }
        }

        private static void ValidateSlotKey(string slotKey)
        {
            if (Array.IndexOf(KnownSlots, slotKey) < 0)
                throw new ArgumentException("Slot must be one of: slot1, slot2, slot3, slot4, slot5.", nameof(slotKey));
        }

        private static int SecondsFromString(string? s)
            => int.TryParse(s, out var x) ? x : 3600;

        private static string Truncate(string s, int max)
            => s.Length <= max ? s : s.Substring(0, max) + "...";

        // -------------------------
        // Models & JSON
        // -------------------------

        private static readonly string[] KnownSlots = { "slot1", "slot2", "slot3", "slot4", "slot5" };

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private static readonly JsonSerializerOptions JsonOptsIndented = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private struct ModlistNode
        {
            [JsonPropertyName("content")]
            public JsonElement Content { get; set; }
        }

        private sealed class AuthState
        {
            public string? Uid { get; set; }
            public string? IdToken { get; set; }
            public string? RefreshToken { get; set; }
            public DateTimeOffset? IdTokenExpiryUtc { get; set; }
        }

        private sealed class AnonAuthResponse
        {
            public string? idToken { get; set; }
            public string? localId { get; set; }
            public string? refreshToken { get; set; }
            public string? expiresIn { get; set; }
        }

        private sealed class RefreshResponse
        {
            public string? id_token { get; set; }
            public string? refresh_token { get; set; }
            public string? expires_in { get; set; }
            public string? user_id { get; set; }
        }
    }
}
