using System.Collections.Generic;

namespace MarkdownEditor.Services;

/// <summary>
/// Strongly-typed application settings.
/// Add new properties freely; missing values use defaults at load time.
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// Versioning marker for future schema migrations.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    // ----------------------------------------------------------------------
    // Appearance
    // ----------------------------------------------------------------------

    /// <summary>
    /// One of: "System", "Light", "Dark".
    /// </summary>
    public string Theme { get; set; } = "System";

    public string EditorFontFamily { get; set; } = "Consolas";
    public double EditorFontSize { get; set; } = 15.0;

    public string PreviewFontFamily { get; set; } = "Segoe UI";
    public double PreviewFontSize { get; set; } = 15.0;

    // ----------------------------------------------------------------------
    // Editor behavior
    // ----------------------------------------------------------------------

    public int AutosaveIntervalSeconds { get; set; } = 5;
    public int MaxBackups { get; set; } = 25;
    public bool RestoreSessionOnLaunch { get; set; } = true;
    public bool ShowPreviewOnLaunch { get; set; } = true;
    public bool ShowEditorOnLaunch { get; set; } = true;

    // ----------------------------------------------------------------------
    // Window state
    // ----------------------------------------------------------------------

    public int WindowWidth { get; set; } = 1280;
    public int WindowHeight { get; set; } = 800;
    public int WindowLeft { get; set; } = -1;   // -1 = let OS decide
    public int WindowTop { get; set; } = -1;
    public bool WindowMaximized { get; set; } = false;

    // ----------------------------------------------------------------------
    // Recent files
    // ----------------------------------------------------------------------

    public int MaxRecentFiles { get; set; } = 10;
    public List<string> RecentFiles { get; set; } = new List<string>();
}