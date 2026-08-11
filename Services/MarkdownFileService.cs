using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MarkdownEditor.Helpers;
using MarkdownEditor.Models;

namespace MarkdownEditor.Services;

/// <summary>
/// Default file service for Markdown documents.
/// Reads, writes, and backs up .md content with atomic writes.
/// </summary>
public sealed class MarkdownFileService : IMarkdownFileService
{
    // ----------------------------------------------------------------------
    // Configuration
    // ----------------------------------------------------------------------

    private const string Filter =
        "Markdown Files (*.md;*.markdown)|*.md;*.markdown|Text Files (*.txt)|*.txt|All Files (*.*)|*.*";

    // ----------------------------------------------------------------------
    // Dependencies
    // ----------------------------------------------------------------------

    private readonly ILoggingService? _logger;
    private readonly ISettingsService? _settings;

    // ----------------------------------------------------------------------
    // Construction
    // ----------------------------------------------------------------------

    public MarkdownFileService(
        ILoggingService? logger = null,
        ISettingsService? settings = null)
    {
        _logger = logger;
        _settings = settings;

        AppFolders.Initialize();
    }

    // ----------------------------------------------------------------------
    // Public API
    // ----------------------------------------------------------------------

    public Task<OpenFileResult?> OpenAsync(IntPtr ownerHandle)
    {
        return Task.Run(() =>
        {
            string? path = Win32FileDialogs.ShowOpenDialog(
                ownerHandle: ownerHandle,
                title: "Open Markdown File",
                filter: Filter,
                initialDirectory: GetInitialDirectory());

            if (string.IsNullOrWhiteSpace(path))
            {
                _logger?.Debug("Open canceled.", "FileService");
                return (OpenFileResult?)null;
            }

            try
            {
                string content = File.ReadAllText(path, Encoding.UTF8);

                _logger?.Info($"Opened: {path}", "FileService");

                if (_settings != null)
                {
                    _settings.AddRecentFile(path);
                    _settings.Save();
                }

                return new OpenFileResult
                {
                    FilePath = path,
                    Content = content
                };
            }
            catch (Exception ex)
            {
                _logger?.Error($"Failed to open: {path}", ex, "FileService");
                return null;
            }
        });
    }

    public Task<bool> SaveAsync(MarkdownDocument document, IntPtr ownerHandle)
    {
        if (document == null)
        {
            return Task.FromResult(false);
        }

        if (string.IsNullOrWhiteSpace(document.FilePath))
        {
            return SaveAsCoreAsync(document, ownerHandle, asSaveAs: true)
                .ContinueWith(t => !string.IsNullOrWhiteSpace(t.Result));
        }

        return SaveCoreAsync(document, document.FilePath!);
    }

    public Task<string?> SaveAsAsync(MarkdownDocument document, IntPtr ownerHandle)
    {
        if (document == null)
        {
            return Task.FromResult<string?>(null);
        }

        return SaveAsCoreAsync(document, ownerHandle, asSaveAs: true);
    }

    // ----------------------------------------------------------------------
    // Internals
    // ----------------------------------------------------------------------

    private Task<string?> SaveAsCoreAsync(
        MarkdownDocument document,
        IntPtr ownerHandle,
        bool asSaveAs)
    {
        return Task.Run<string?>(() =>
        {
            string suggested = !string.IsNullOrWhiteSpace(document.Title)
                ? document.Title
                : "Untitled.md";

            string? path = Win32FileDialogs.ShowSaveDialog(
                ownerHandle: ownerHandle,
                title: asSaveAs ? "Save Markdown As" : "Save Markdown",
                filter: Filter,
                initialDirectory: GetInitialDirectory(),
                suggestedFileName: suggested);

            if (string.IsNullOrWhiteSpace(path))
            {
                _logger?.Debug("Save canceled.", "FileService");
                return null;
            }

            bool success = SaveCoreAsync(document, path).GetAwaiter().GetResult();
            return success ? path : null;
        });
    }

    private Task<bool> SaveCoreAsync(MarkdownDocument document, string path)
    {
        return Task.Run(() =>
        {
            try
            {
                CreateBackupIfNeeded(path);
                WriteAtomically(path, document.Content);

                document.FilePath = path;
                document.MarkSaved();

                _logger?.Info($"Saved: {path}", "FileService");

                if (_settings != null)
                {
                    _settings.AddRecentFile(path);
                    _settings.Save();
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger?.Error($"Failed to save: {path}", ex, "FileService");
                return false;
            }
        });
    }

    private void WriteAtomically(string finalPath, string content)
    {
        string directory = Path.GetDirectoryName(finalPath) ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempPath = finalPath + ".tmp";
        string backupPath = finalPath + ".bak";

        File.WriteAllText(tempPath, content ?? string.Empty, new UTF8Encoding(false));

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
                // Best-effort cleanup.
            }
        }
        else
        {
            File.Move(tempPath, finalPath);
        }
    }

    private void CreateBackupIfNeeded(string sourcePath)
    {
        try
        {
            if (!File.Exists(sourcePath))
            {
                return;
            }

            string backupsFolder = AppFolders.Backups;
            Directory.CreateDirectory(backupsFolder);

            string baseName = Path.GetFileNameWithoutExtension(sourcePath);
            string extension = Path.GetExtension(sourcePath);
            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string backupName = $"{baseName}-{timestamp}{extension}.bak";

            string backupPath = Path.Combine(backupsFolder, backupName);

            File.Copy(sourcePath, backupPath, overwrite: true);

            _logger?.Debug($"Backup created: {backupPath}", "FileService");

            TrimOldBackups(backupsFolder);
        }
        catch (Exception ex)
        {
            _logger?.Warn($"Backup failed for {sourcePath}: {ex.Message}", "FileService");
        }
    }

    private void TrimOldBackups(string backupsFolder)
    {
        try
        {
            int max = _settings?.Current.MaxBackups ?? 25;

            if (max < 1)
            {
                return;
            }

            string[] files = Directory.GetFiles(backupsFolder, "*.bak", SearchOption.TopDirectoryOnly);

            if (files.Length <= max)
            {
                return;
            }

            var ordered = files
                .Select(f => new FileInfo(f))
                .OrderByDescending(fi => fi.LastWriteTimeUtc)
                .ToList();

            for (int i = max; i < ordered.Count; i++)
            {
                try
                {
                    ordered[i].Delete();
                }
                catch
                {
                    // Ignore single-file failure.
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.Warn($"Backup trim failed: {ex.Message}", "FileService");
        }
    }

    private string GetInitialDirectory()
    {
        try
        {
            string? recent = _settings?.Current.RecentFiles.FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(recent))
            {
                string? dir = Path.GetDirectoryName(recent);
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                {
                    return dir;
                }
            }
        }
        catch
        {
            // Fall through to default.
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }
}