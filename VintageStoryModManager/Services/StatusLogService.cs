using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace VintageStoryModManager.Services;

/// <summary>
/// Writes status updates from the UI to a log file so that the recent history can be reviewed.
/// </summary>
public static class StatusLogService
{
    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";
    private static readonly object SyncRoot = new();
    private static readonly string LogFilePath = InitializeLogFilePath();

    private static string InitializeLogFilePath()
    {
        try
        {
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!string.IsNullOrEmpty(documentsPath))
            {
                string directory = Path.Combine(documentsPath, "VS Mod Manager");
                Directory.CreateDirectory(directory);
                return Path.Combine(directory, "VSModManagerStatus.log");
            }
        }
        catch (Exception)
        {
            // Ignore failures when determining the documents directory and fall back to the app directory.
        }

        return Path.Combine(AppContext.BaseDirectory, "VSModManagerStatus.log");
    }

    /// <summary>
    /// Appends a status entry to the log file using a timestamp and severity marker.
    /// </summary>
    /// <param name="message">The message to record.</param>
    /// <param name="isError">Whether the status represents an error.</param>
    public static void AppendStatus(string message, bool isError)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        string timestamp = DateTime.Now.ToString(TimestampFormat, CultureInfo.InvariantCulture);
        string severity = isError ? "ERROR" : "INFO";
        string line = $"[{timestamp}] [{severity}] {message}{Environment.NewLine}";

        try
        {
            lock (SyncRoot)
            {
                File.AppendAllText(LogFilePath, line, Encoding.UTF8);
            }
        }
        catch (IOException)
        {
            // Ignore logging failures so that the UI does not crash when the log cannot be written.
        }
        catch (UnauthorizedAccessException)
        {
            // Ignore logging failures so that the UI does not crash when the log cannot be written.
        }
    }
}
