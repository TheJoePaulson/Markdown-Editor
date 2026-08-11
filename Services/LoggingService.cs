using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

using MarkdownEditor.Helpers;

namespace MarkdownEditor.Services;

/// <summary>
/// File-based logger with rotation and thread-safe writes.
/// 
/// Default location:
///     [ProfileRoot]\Logs\Application.log
///
/// When file exceeds <see cref="MaxFileSizeBytes"/>, it rotates:
///     Application.log → Application.1.log → Application.2.log → ...
/// up to <see cref="MaxRotatedFiles"/> retained.
/// </summary>
public sealed class LoggingService : ILoggingService
{
    // ----------------------------------------------------------------------
    // Configuration
    // ----------------------------------------------------------------------

    private const string LogFileName = "Application.log";
    private const long MaxFileSizeBytes = 2 * 1024 * 1024; // 2 MB
    private const int MaxRotatedFiles = 5;

    // ----------------------------------------------------------------------
    // State
    // ----------------------------------------------------------------------

    private readonly object _gate = new object();
    private readonly string _logDirectory;
    private readonly string _logFilePath;
    private readonly LogLevel _minimumLevel;

    // ----------------------------------------------------------------------
    // Properties
    // ----------------------------------------------------------------------

    public string CurrentLogFilePath => _logFilePath;

    // ----------------------------------------------------------------------
    // Construction
    // ----------------------------------------------------------------------

    public LoggingService(LogLevel minimumLevel = LogLevel.Debug)
    {
        AppFolders.Initialize();

        _logDirectory = AppFolders.Logs;
        _logFilePath = Path.Combine(_logDirectory, LogFileName);
        _minimumLevel = minimumLevel;

        try
        {
            Directory.CreateDirectory(_logDirectory);
        }
        catch
        {
            // Logging must never throw at startup.
        }
    }

    // ----------------------------------------------------------------------
    // Public API
    // ----------------------------------------------------------------------

    public void Debug(string message, string? category = null)
    {
        Write(LogLevel.Debug, message, null, category);
    }

    public void Info(string message, string? category = null)
    {
        Write(LogLevel.Info, message, null, category);
    }

    public void Warn(string message, string? category = null)
    {
        Write(LogLevel.Warn, message, null, category);
    }

    public void Error(string message, Exception? exception = null, string? category = null)
    {
        Write(LogLevel.Error, message, exception, category);
    }

    public void Flush()
    {
        // Currently writes are synchronous; nothing to flush.
    }

    // ----------------------------------------------------------------------
    // Internals
    // ----------------------------------------------------------------------

    private void Write(LogLevel level, string message, Exception? exception, string? category)
    {
        if (level < _minimumLevel)
        {
            return;
        }

        string formatted = Format(level, message, exception, category);

        // Always echo to debugger for development visibility.
        Trace.WriteLine(formatted);

        lock (_gate)
        {
            try
            {
                RotateIfNeeded();

                File.AppendAllText(
                    _logFilePath,
                    formatted + Environment.NewLine,
                    Encoding.UTF8);
            }
            catch
            {
                // Never let logging crash the application.
            }
        }
    }

    private static string Format(
        LogLevel level,
        string message,
        Exception? exception,
        string? category)
    {
        string timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");
        string levelText = LevelToString(level);
        string cat = string.IsNullOrWhiteSpace(category) ? "App" : category!;
        int threadId = Thread.CurrentThread.ManagedThreadId;

        StringBuilder sb = new StringBuilder();

        sb.Append('[').Append(timestamp).Append(']');
        sb.Append(' ').Append('[').Append(levelText).Append(']');
        sb.Append(' ').Append('[').Append("T").Append(threadId).Append(']');
        sb.Append(' ').Append('[').Append(cat).Append(']');
        sb.Append(' ').Append(message);

        if (exception != null)
        {
            sb.AppendLine();
            sb.Append("    EXCEPTION: ").Append(exception.GetType().FullName).AppendLine();
            sb.Append("    MESSAGE  : ").Append(exception.Message).AppendLine();
            sb.Append("    STACK    : ").Append(exception.StackTrace);
        }

        return sb.ToString();
    }

    private static string LevelToString(LogLevel level)
    {
        return level switch
        {
            LogLevel.Debug => "DEBUG",
            LogLevel.Info => "INFO ",
            LogLevel.Warn => "WARN ",
            LogLevel.Error => "ERROR",
            _ => "UNKWN"
        };
    }

    private void RotateIfNeeded()
    {
        try
        {
            FileInfo info = new FileInfo(_logFilePath);

            if (!info.Exists)
            {
                return;
            }

            if (info.Length < MaxFileSizeBytes)
            {
                return;
            }

            // Rotate: shift older files up (.4 → .5, .3 → .4, …)
            for (int i = MaxRotatedFiles; i >= 1; i--)
            {
                string older = GetRotatedFile(i);
                string newer = GetRotatedFile(i - 1);

                if (i == MaxRotatedFiles && File.Exists(older))
                {
                    File.Delete(older);
                }

                if (File.Exists(newer))
                {
                    File.Move(newer, older, overwrite: true);
                }
            }

            // Move current → .1
            string firstRotation = GetRotatedFile(1);

            if (File.Exists(firstRotation))
            {
                File.Delete(firstRotation);
            }

            File.Move(_logFilePath, firstRotation);
        }
        catch
        {
            // Rotation must never crash the app.
        }
    }

    private string GetRotatedFile(int index)
    {
        // index 0 = current "Application.log"
        if (index <= 0)
        {
            return _logFilePath;
        }

        string fileName = $"Application.{index}.log";
        return Path.Combine(_logDirectory, fileName);
    }
}