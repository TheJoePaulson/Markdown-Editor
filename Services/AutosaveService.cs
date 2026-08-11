using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using MarkdownEditor.Helpers;
using MarkdownEditor.Models;

namespace MarkdownEditor.Services;

/// <summary>
/// Default autosave implementation.
/// Saves drafts as JSON to AppFolders.Autosave with one file per document.
/// Uses a debounce timer to coalesce rapid edits into a single write.
/// </summary>
public sealed class AutosaveService : IAutosaveService, IDisposable
{
    // ----------------------------------------------------------------------
    // Configuration
    // ----------------------------------------------------------------------

    private const string DraftFilePrefix = "Draft-";
    private const string DraftFileExtension = ".json";

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    // ----------------------------------------------------------------------
    // Dependencies / state
    // ----------------------------------------------------------------------

    private readonly object _gate = new object();
    private readonly ILoggingService? _logger;
    private readonly ISettingsService? _settings;
    private readonly string _autosaveFolder;
    private readonly Dictionary<string, Timer> _timersByDocumentId = new Dictionary<string, Timer>();
    private bool _disposed;

    // ----------------------------------------------------------------------
    // Properties
    // ----------------------------------------------------------------------

    public string AutosaveFolderPath => _autosaveFolder;

    // ----------------------------------------------------------------------
    // Construction
    // ----------------------------------------------------------------------

    public AutosaveService(ILoggingService? logger = null, ISettingsService? settings = null)
    {
        _logger = logger;
        _settings = settings;

        AppFolders.Initialize();
        _autosaveFolder = AppFolders.Autosave;

        try
        {
            Directory.CreateDirectory(_autosaveFolder);
        }
        catch
        {
            // Autosave must never crash startup.
        }

        _logger?.Info($"Autosave folder: {_autosaveFolder}", "Autosave");
    }

    // ----------------------------------------------------------------------
    // Public API
    // ----------------------------------------------------------------------

    public void ScheduleAutosave(MarkdownDocument document)
    {
        if (document == null)
        {
            return;
        }

        int seconds = GetAutosaveIntervalSeconds();

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            if (_timersByDocumentId.TryGetValue(document.DocumentId, out Timer? existing))
            {
                existing.Change(TimeSpan.FromSeconds(seconds), Timeout.InfiniteTimeSpan);
                return;
            }

            Timer timer = new Timer(
                callback: state => OnTimerTick(document),
                state: null,
                dueTime: TimeSpan.FromSeconds(seconds),
                period: Timeout.InfiniteTimeSpan);

            _timersByDocumentId[document.DocumentId] = timer;
        }
    }

    public async Task ForceSaveAsync(MarkdownDocument document)
    {
        if (document == null)
        {
            return;
        }

        CancelTimer(document.DocumentId);

        await SaveSnapshotAsync(document).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<MarkdownDocumentSnapshot>> LoadDraftsAsync()
    {
        return Task.Run<IReadOnlyList<MarkdownDocumentSnapshot>>(() =>
        {
            List<MarkdownDocumentSnapshot> results = new List<MarkdownDocumentSnapshot>();

            try
            {
                if (!Directory.Exists(_autosaveFolder))
                {
                    return results;
                }

                string[] files = Directory.GetFiles(
                    _autosaveFolder,
                    DraftFilePrefix + "*" + DraftFileExtension,
                    SearchOption.TopDirectoryOnly);

                foreach (string file in files)
                {
                    try
                    {
                        string json = File.ReadAllText(file, Encoding.UTF8);

                        if (string.IsNullOrWhiteSpace(json))
                        {
                            continue;
                        }

                        MarkdownDocumentSnapshot? snap =
                            JsonSerializer.Deserialize<MarkdownDocumentSnapshot>(json, JsonOptions);

                        if (snap != null)
                        {
                            results.Add(snap);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.Warn(
                            $"Skipping unreadable draft: {file} ({ex.Message})",
                            "Autosave");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Error("Failed to enumerate drafts.", ex, "Autosave");
            }

            _logger?.Info($"Loaded {results.Count} draft(s) from disk.", "Autosave");
            return results;
        });
    }

    public Task ClearDraftAsync(MarkdownDocument document)
    {
        if (document == null)
        {
            return Task.CompletedTask;
        }

        CancelTimer(document.DocumentId);

        return Task.Run(() =>
        {
            try
            {
                string path = GetDraftPath(document.DocumentId);

                if (File.Exists(path))
                {
                    File.Delete(path);
                    _logger?.Debug($"Cleared draft for {document.DocumentId}.", "Autosave");
                }
            }
            catch (Exception ex)
            {
                _logger?.Warn(
                    $"Failed to clear draft for {document.DocumentId}: {ex.Message}",
                    "Autosave");
            }
        });
    }

    public Task ClearAllDraftsAsync()
    {
        return Task.Run(() =>
        {
            CancelAllTimers();

            try
            {
                if (!Directory.Exists(_autosaveFolder))
                {
                    return;
                }

                string[] files = Directory.GetFiles(
                    _autosaveFolder,
                    DraftFilePrefix + "*" + DraftFileExtension,
                    SearchOption.TopDirectoryOnly);

                foreach (string file in files)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                        // Ignore per-file failure; continue.
                    }
                }

                _logger?.Info("Cleared all autosave drafts.", "Autosave");
            }
            catch (Exception ex)
            {
                _logger?.Error("Failed to clear all drafts.", ex, "Autosave");
            }
        });
    }

    // ----------------------------------------------------------------------
    // Internals
    // ----------------------------------------------------------------------

    private void OnTimerTick(MarkdownDocument document)
    {
        try
        {
            SaveSnapshotAsync(document).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger?.Error("Autosave timer tick failed.", ex, "Autosave");
        }
        finally
        {
            CancelTimer(document.DocumentId);
        }
    }

    private Task SaveSnapshotAsync(MarkdownDocument document)
    {
        return Task.Run(() =>
        {
            MarkdownDocumentSnapshot snapshot = document.ToSnapshot();

            try
            {
                WriteAtomically(snapshot);

                document.MarkAutosaved();

                _logger?.Debug(
                    $"Autosaved draft for {snapshot.DocumentId} ({snapshot.Content.Length} chars).",
                    "Autosave");
            }
            catch (Exception ex)
            {
                _logger?.Error(
                    $"Failed to autosave draft for {snapshot.DocumentId}.",
                    ex,
                    "Autosave");
            }
        });
    }

    private void WriteAtomically(MarkdownDocumentSnapshot snapshot)
    {
        Directory.CreateDirectory(_autosaveFolder);

        string finalPath = GetDraftPath(snapshot.DocumentId);
        string tempPath = finalPath + ".tmp";
        string backupPath = finalPath + ".bak";

        string json = JsonSerializer.Serialize(snapshot, JsonOptions);

        File.WriteAllText(tempPath, json, Encoding.UTF8);

        if (File.Exists(finalPath))
        {
            File.Replace(tempPath, finalPath, backupPath, ignoreMetadataErrors: true);

            try
            {
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
            }
            catch
            {
                // Best-effort backup cleanup; ignore.
            }
        }
        else
        {
            File.Move(tempPath, finalPath);
        }
    }

    private string GetDraftPath(string documentId)
    {
        string safeId = SanitizeId(documentId);
        string name = DraftFilePrefix + safeId + DraftFileExtension;
        return Path.Combine(_autosaveFolder, name);
    }

    private static string SanitizeId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Guid.NewGuid().ToString("N");
        }

        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            id = id.Replace(invalid, '_');
        }

        return id;
    }

    private int GetAutosaveIntervalSeconds()
    {
        int seconds = _settings?.Current.AutosaveIntervalSeconds ?? 5;

        if (seconds < 1)
        {
            seconds = 1;
        }
        else if (seconds > 600)
        {
            seconds = 600;
        }

        return seconds;
    }

    private void CancelTimer(string documentId)
    {
        lock (_gate)
        {
            if (_timersByDocumentId.TryGetValue(documentId, out Timer? timer))
            {
                _timersByDocumentId.Remove(documentId);

                try
                {
                    timer.Dispose();
                }
                catch
                {
                    // Ignore.
                }
            }
        }
    }

    private void CancelAllTimers()
    {
        lock (_gate)
        {
            foreach (KeyValuePair<string, Timer> kvp in _timersByDocumentId)
            {
                try
                {
                    kvp.Value.Dispose();
                }
                catch
                {
                    // Ignore.
                }
            }

            _timersByDocumentId.Clear();
        }
    }

    // ----------------------------------------------------------------------
    // IDisposable
    // ----------------------------------------------------------------------

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        CancelAllTimers();
    }
}
