using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace VintageStoryModManager.Services;

/// <summary>
/// Provides a lightweight HTTP proxy for the Vintage Story mod database that normalizes
/// the <c>tagid</c> field of the <c>gameversions</c> response to 32-bit integers. The proxy
/// enables older servers to successfully parse the response when executing mod install
/// commands from generated macros.
/// </summary>
internal static class ModDbProxyService
{
    private const string DefaultProxyPrefix = "http://127.0.0.1:21552/";
    private static readonly Uri UpstreamBaseUri = new("https://mods.vintagestory.at/");
    private static readonly HttpClient HttpClient = new();

    private static readonly ConcurrentDictionary<long, int> TagIdMap = new();
    private static int _nextTagId = 0;

    private static readonly object SyncRoot = new();
    private static HttpListener? _listener;
    private static CancellationTokenSource? _listenerCancellation;
    private static Task? _listenerTask;

    public static string ProxyBaseAddress => DefaultProxyPrefix;

    public static bool IsRunning
    {
        get
        {
            lock (SyncRoot)
            {
                return _listener is not null && _listenerTask is { IsCompleted: false };
            }
        }
    }

    /// <summary>
    /// Ensures that the proxy is running. When the proxy cannot be started the error
    /// message is returned via <paramref name="errorMessage"/>.
    /// </summary>
    public static bool TryEnsureRunning(out string? errorMessage)
    {
        lock (SyncRoot)
        {
            if (IsRunning)
            {
                errorMessage = null;
                return true;
            }

            try
            {
                var listener = new HttpListener();
                listener.Prefixes.Add(DefaultProxyPrefix);
                listener.Prefixes.Add("http://localhost:21552/");
                listener.Start();

                _listenerCancellation = new CancellationTokenSource();
                CancellationToken token = _listenerCancellation.Token;
                _listener = listener;
                _listenerTask = Task.Run(() => RunListenerAsync(listener, token), token);

                errorMessage = null;
                return true;
            }
            catch (HttpListenerException ex)
            {
                errorMessage = ex.Message;
                StopInternal();
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                StopInternal();
                return false;
            }
        }
    }

    public static void Stop()
    {
        lock (SyncRoot)
        {
            StopInternal();
        }
    }

    private static void StopInternal()
    {
        try
        {
            _listenerCancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Ignore.
        }

        try
        {
            _listener?.Stop();
        }
        catch (ObjectDisposedException)
        {
            // Ignore.
        }

        _listener = null;
        _listenerCancellation?.Dispose();
        _listenerCancellation = null;
        _listenerTask = null;
        TagIdMap.Clear();
        _nextTagId = 0;
    }

    private static async Task RunListenerAsync(HttpListener listener, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                HttpListenerContext? context = null;
                try
                {
                    context = await listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (HttpListenerException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                if (context is null)
                {
                    continue;
                }

                _ = Task.Run(() => HandleRequestAsync(context, token), token);
            }
        }
        finally
        {
            listener.Close();
        }
    }

    private static async Task HandleRequestAsync(HttpListenerContext context, CancellationToken token)
    {
        try
        {
            Uri requestUri = BuildUpstreamUri(context.Request.RawUrl);
            using var upstreamRequest = new HttpRequestMessage(new HttpMethod(context.Request.HttpMethod), requestUri);

            if (context.Request.HasEntityBody)
            {
                using Stream bodyStream = context.Request.InputStream;
                using var reader = new StreamReader(bodyStream, context.Request.ContentEncoding);
                string body = await reader.ReadToEndAsync().ConfigureAwait(false);
                upstreamRequest.Content = new StringContent(body, context.Request.ContentEncoding);
            }

            using HttpResponseMessage upstreamResponse = await HttpClient.SendAsync(upstreamRequest, token).ConfigureAwait(false);

            byte[] responseBytes;
            string contentType = upstreamResponse.Content.Headers.ContentType?.ToString() ?? "application/json";

            if (IsGameVersionsRequest(requestUri))
            {
                string json = await upstreamResponse.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                string sanitized = SanitizeGameVersions(json);
                responseBytes = Encoding.UTF8.GetBytes(sanitized);
                contentType = "application/json";
            }
            else
            {
                responseBytes = await upstreamResponse.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false);
            }

            context.Response.StatusCode = (int)upstreamResponse.StatusCode;
            context.Response.ContentType = contentType;
            context.Response.ContentLength64 = responseBytes.Length;
            await context.Response.OutputStream.WriteAsync(responseBytes, 0, responseBytes.Length, token).ConfigureAwait(false);
        }
        catch (Exception ex) when (!token.IsCancellationRequested)
        {
            byte[] message = Encoding.UTF8.GetBytes($"Proxy error: {ex.Message}");
            context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
            context.Response.ContentType = "text/plain";
            context.Response.ContentLength64 = message.Length;
            await context.Response.OutputStream.WriteAsync(message, 0, message.Length, token).ConfigureAwait(false);
        }
        finally
        {
            context.Response.OutputStream.Close();
        }
    }

    private static Uri BuildUpstreamUri(string? rawUrl)
    {
        if (string.IsNullOrEmpty(rawUrl))
        {
            return UpstreamBaseUri;
        }

        if (rawUrl.StartsWith('/'))
        {
            return new Uri(UpstreamBaseUri, rawUrl);
        }

        return new Uri(UpstreamBaseUri, "/" + rawUrl);
    }

    private static bool IsGameVersionsRequest(Uri uri)
    {
        string path = uri.AbsolutePath;
        return path.Equals("/api/gameversions", StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeGameVersions(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                foreach (JsonProperty property in root.EnumerateObject())
                {
                    if (property.NameEquals("gameversions") && property.Value.ValueKind == JsonValueKind.Array)
                    {
                        writer.WritePropertyName(property.Name);
                        writer.WriteStartArray();
                        foreach (JsonElement entry in property.Value.EnumerateArray())
                        {
                            writer.WriteStartObject();
                            foreach (JsonProperty entryProperty in entry.EnumerateObject())
                            {
                                if (entryProperty.NameEquals("tagid") && entryProperty.Value.ValueKind == JsonValueKind.Number)
                                {
                                    if (entryProperty.Value.TryGetInt64(out long original))
                                    {
                                        int mapped = GetOrCreateMappedTagId(original);
                                        writer.WriteNumber(entryProperty.Name, mapped);
                                    }
                                    else
                                    {
                                        writer.WriteNumber(entryProperty.Name, GetOrCreateMappedTagId(0));
                                    }
                                }
                                else
                                {
                                    entryProperty.WriteTo(writer);
                                }
                            }

                            writer.WriteEndObject();
                        }

                        writer.WriteEndArray();
                    }
                    else
                    {
                        property.WriteTo(writer);
                    }
                }

                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static int GetOrCreateMappedTagId(long original)
    {
        if (TagIdMap.TryGetValue(original, out int existing))
        {
            return existing;
        }

        int next = Interlocked.Increment(ref _nextTagId);
        int mapped = next == int.MinValue ? 1 : next;
        TagIdMap.TryAdd(original, mapped);
        return mapped;
    }
}
