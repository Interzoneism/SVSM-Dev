using System;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VintageStoryModManager.Services;

/// <summary>
/// Provides helpers for monitoring outbound HTTP traffic initiated by the application.
/// </summary>
public static class InternetTrafficTracker
{
    /// <summary>
    /// Raised whenever an HTTP request completes (successfully or unsuccessfully).
    /// </summary>
    public static event EventHandler<InternetTrafficEventArgs>? TrafficRecorded;

    /// <summary>
    /// Creates an <see cref="HttpClient"/> instance that will report network traffic for the specified process.
    /// </summary>
    /// <param name="processName">A descriptive name for the logical process that owns the client.</param>
    /// <param name="innerHandler">An optional inner handler to wrap.</param>
    /// <returns>A configured <see cref="HttpClient"/> that reports network traffic.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="processName"/> is null or whitespace.</exception>
    public static HttpClient CreateHttpClient(string processName, HttpMessageHandler? innerHandler = null)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            throw new ArgumentException("Process name must not be null or whitespace.", nameof(processName));
        }

        HttpMessageHandler handler = innerHandler ?? new HttpClientHandler();
        return new HttpClient(new TrackingHandler(processName.Trim(), handler), disposeHandler: true);
    }

    internal static void RecordEvent(InternetTrafficEventArgs args)
    {
        if (args is null)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.Append("[Network]");
        builder.Append('[').Append(args.ProcessName).Append(']');
        builder.Append(' ').Append(args.Method?.Method ?? "(unknown)");

        if (args.RequestUri is not null)
        {
            builder.Append(' ').Append(args.RequestUri);
        }

        builder.Append(" start=")
            .Append(args.StartTimestamp.ToString("o", CultureInfo.InvariantCulture));
        builder.Append(" duration=")
            .Append(((long)args.Duration.TotalMilliseconds).ToString(CultureInfo.InvariantCulture))
            .Append("ms");

        if (args.StatusCode is not null)
        {
            builder.Append(" status=")
                .Append(((int)args.StatusCode.Value).ToString(CultureInfo.InvariantCulture))
                .Append(' ')
                .Append(args.StatusCode.Value);
        }

        if (args.RequestContentLength is not null)
        {
            builder.Append(" sent=")
                .Append(args.RequestContentLength.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (args.ResponseContentLength is not null)
        {
            builder.Append(" received=")
                .Append(args.ResponseContentLength.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (args.Error is not null)
        {
            builder.Append(" error=")
                .Append(args.Error.GetType().Name)
                .Append(':')
                .Append(' ')
                .Append(args.Error.Message);
        }

        StatusLogService.AppendStatus(builder.ToString(), args.IsError);
        TrafficRecorded?.Invoke(null, args);
    }

    private sealed class TrackingHandler : DelegatingHandler
    {
        private readonly string _processName;

        public TrackingHandler(string processName, HttpMessageHandler innerHandler)
            : base(innerHandler ?? throw new ArgumentNullException(nameof(innerHandler)))
        {
            _processName = processName;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            DateTimeOffset startTimestamp = DateTimeOffset.UtcNow;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                stopwatch.Stop();

                var args = new InternetTrafficEventArgs(
                    _processName,
                    request.Method,
                    request.RequestUri,
                    response.StatusCode,
                    !response.IsSuccessStatusCode,
                    stopwatch.Elapsed,
                    startTimestamp,
                    request.Content?.Headers.ContentLength,
                    response.Content?.Headers.ContentLength,
                    null);

                RecordEvent(args);
                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                bool isCancellation = cancellationToken.IsCancellationRequested
                    && ex is OperationCanceledException;

                var args = new InternetTrafficEventArgs(
                    _processName,
                    request.Method,
                    request.RequestUri,
                    null,
                    !isCancellation,
                    stopwatch.Elapsed,
                    startTimestamp,
                    request.Content?.Headers.ContentLength,
                    null,
                    ex);

                RecordEvent(args);
                throw;
            }
        }
    }
}

/// <summary>
/// Provides data describing a single HTTP request performed by the application.
/// </summary>
public sealed class InternetTrafficEventArgs : EventArgs
{
    public InternetTrafficEventArgs(
        string processName,
        HttpMethod method,
        Uri? requestUri,
        HttpStatusCode? statusCode,
        bool isError,
        TimeSpan duration,
        DateTimeOffset startTimestamp,
        long? requestContentLength,
        long? responseContentLength,
        Exception? error)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            throw new ArgumentException("Process name must not be null or whitespace.", nameof(processName));
        }

        ProcessName = processName.Trim();
        Method = method ?? throw new ArgumentNullException(nameof(method));
        RequestUri = requestUri;
        StatusCode = statusCode;
        IsError = isError;
        Duration = duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
        StartTimestamp = startTimestamp;
        RequestContentLength = requestContentLength;
        ResponseContentLength = responseContentLength;
        Error = error;
    }

    /// <summary>
    /// Gets the logical process name that initiated the request.
    /// </summary>
    public string ProcessName { get; }

    /// <summary>
    /// Gets the HTTP method that was used.
    /// </summary>
    public HttpMethod Method { get; }

    /// <summary>
    /// Gets the requested URI, if available.
    /// </summary>
    public Uri? RequestUri { get; }

    /// <summary>
    /// Gets the HTTP status code returned by the server, if a response was received.
    /// </summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>
    /// Gets a value indicating whether the request ended with an error.
    /// </summary>
    public bool IsError { get; }

    /// <summary>
    /// Gets the elapsed time between the request starting and completing.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Gets the timestamp indicating when the request started.
    /// </summary>
    public DateTimeOffset StartTimestamp { get; }

    /// <summary>
    /// Gets the number of bytes sent in the request body, if known.
    /// </summary>
    public long? RequestContentLength { get; }

    /// <summary>
    /// Gets the number of bytes reported in the response body, if known.
    /// </summary>
    public long? ResponseContentLength { get; }

    /// <summary>
    /// Gets the exception that caused the request to fail, if any.
    /// </summary>
    public Exception? Error { get; }
}
