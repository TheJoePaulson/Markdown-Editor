using System;

namespace MarkdownEditor.Services;

/// <summary>
/// Contract for the application logger. Implementations must be thread-safe.
/// </summary>
public interface ILoggingService
{
    void Debug(string message, string? category = null);
    void Info(string message, string? category = null);
    void Warn(string message, string? category = null);
    void Error(string message, Exception? exception = null, string? category = null);

    /// <summary>
    /// Returns the absolute path of the current active log file.
    /// </summary>
    string CurrentLogFilePath { get; }

    /// <summary>
    /// Flushes pending writes (best-effort).
    /// </summary>
    void Flush();
}