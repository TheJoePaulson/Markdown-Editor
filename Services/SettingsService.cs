using System;
using System.IO;
using System.Text;
using System.Text.Json;

using MarkdownEditor.Helpers;

namespace MarkdownEditor.Services;

/// <summary>
/// Default settings service backed by a single JSON file under AppFolders.Settings.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    // ----------------------------------------------------------------------
    // Configuration
    // ----------------------------------------------------------------------

    private const string SettingsFileName = "Settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    // ----------------------------------------------------------------------
    // State
    // ----------------------------------------------------------------------

    private readonly object _gate = new object();
    private readonly ILoggingService? _logger;
    private readonly string _filePath;
    private AppSettings _current;

    // ----------------------------------------------------------------------
    // Properties
    // ----------------------------------------------------------------------

    public AppSettings Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public string SettingsFilePath => _filePath;

    // ----------------------------------------------------------------------
    // Construction
    // ----------------------------------------------------------------------

    public SettingsService(ILoggingService? logger = null)
    {
        _logger = logger;

        AppFolders.Initialize();

        string folder = AppFolders.Settings;
        _filePath = Path.Combine(folder, SettingsFileName);

        _current = LoadFromDisk();

        _logger?.Info($"Settings loaded from: {_filePath}", "Settings");
    }

    // ----------------------------------------------------------------------
    // Public API
    // ----------------------------------------------------------------------

    public void Save()
    {
        lock (_gate)
        {
            try
            {
                WriteAtomically(_current);
                _logger?.Debug("Settings saved.", "Settings");
            }
            catch (Exception ex)
            {
                _logger?.Error("Failed to save settings.", ex, "Settings");
            }
        }
    }

    public void Reload()
    {
        lock (_gate)
        {
            _current = LoadFromDisk();
            _logger?.Info("Settings reloaded from disk.", "Settings");
        }
    }

    public void ResetToDefaults()
    {
        lock (_gate)
        {
            _current = new AppSettings();
            Save();
            _logger?.Warn("Settings reset to defaults.", "Settings");
        }
    }

    public void AddRecentFile(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return;
        }

        lock (_gate)
        {
            // Remove existing matches (case-insensitive)
            for (int i = _current.RecentFiles.Count - 1; i >= 0; i--)
            {
                if (string.Equals(
                        _current.RecentFiles[i],
                        fullPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    _current.RecentFiles.RemoveAt(i);
                }
            }

            _current.RecentFiles.Insert(0, fullPath);

            int max = Math.Max(1, _current.MaxRecentFiles);

            while (_current.RecentFiles.Count > max)
            {
                _current.RecentFiles.RemoveAt(_current.RecentFiles.Count - 1);
            }
        }
    }

    // ----------------------------------------------------------------------
    // Disk I/O
    // ----------------------------------------------------------------------

    private AppSettings LoadFromDisk()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                AppSettings defaults = new AppSettings();
                WriteAtomically(defaults);
                return defaults;
            }

            string json = File.ReadAllText(_filePath, Encoding.UTF8);

            if (string.IsNullOrWhiteSpace(json))
            {
                return new AppSettings();
            }

            AppSettings? loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);

            if (loaded == null)
            {
                return new AppSettings();
            }

            // Future migration hook
            if (loaded.SchemaVersion < 1)
            {
                loaded.SchemaVersion = 1;
            }

            return loaded;
        }
        catch (Exception ex)
        {
            _logger?.Error("Failed to load settings; defaults will be used.", ex, "Settings");
            return new AppSettings();
        }
    }

    private void WriteAtomically(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

        string tempPath = _filePath + ".tmp";
        string backupPath = _filePath + ".bak";

        string json = JsonSerializer.Serialize(settings, JsonOptions);

        // 1) Write to temp file first
        File.WriteAllText(tempPath, json, Encoding.UTF8);

        // 2) Atomically replace target
        if (File.Exists(_filePath))
        {
            File.Replace(tempPath, _filePath, backupPath, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(tempPath, _filePath);
        }
    }
}