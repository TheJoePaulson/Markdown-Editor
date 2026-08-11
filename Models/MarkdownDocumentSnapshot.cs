using System;

namespace MarkdownEditor.Models;

/// <summary>
/// Serializable, flat snapshot of a <see cref="MarkdownDocument"/>.
/// Used by the autosave service for round-tripping document state to/from disk.
/// </summary>
public sealed class MarkdownDocumentSnapshot
{
    public string DocumentId { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "Untitled.md";
    public string? FilePath { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsDirty { get; set; }
    public DateTimeOffset? LastSavedUtc { get; set; }
    public DateTimeOffset? LastAutosavedUtc { get; set; }
}