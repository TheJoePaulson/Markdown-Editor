using System;

namespace MarkdownEditor.Models;

/// <summary>
/// Metadata for a single Markdown template discovered on disk.
/// </summary>
public sealed class TemplateInfo
{
    /// <summary>
    /// Display name (filename without extension).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Full path to the template file.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Last modified timestamp (used for sorting "recent" templates if desired).
    /// </summary>
    public DateTime LastModifiedUtc { get; set; }
}