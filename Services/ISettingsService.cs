namespace MarkdownEditor.Services;

/// <summary>
/// Persistent settings service.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// The current in-memory settings instance.
    /// Always non-null after construction.
    /// </summary>
    AppSettings Current { get; }

    /// <summary>
    /// Save the current settings to disk.
    /// </summary>
    void Save();

    /// <summary>
    /// Re-read settings from disk, discarding any unsaved in-memory changes.
    /// </summary>
    void Reload();

    /// <summary>
    /// Reset settings to defaults and persist.
    /// </summary>
    void ResetToDefaults();

    /// <summary>
    /// Add a path to Recent Files, deduping and trimming to MaxRecentFiles.
    /// Caller must call <see cref="Save"/> to persist.
    /// </summary>
    void AddRecentFile(string fullPath);

    /// <summary>
    /// Returns the absolute path of the settings file.
    /// </summary>
    string SettingsFilePath { get; }
}