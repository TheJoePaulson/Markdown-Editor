using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace MarkdownEditor.Models;

/// <summary>
/// Represents a single open Markdown document in the editor.
/// Observable so the UI (via MVVM bindings) reacts to changes.
/// </summary>
public sealed class MarkdownDocument : INotifyPropertyChanged
{
    // ----------------------------------------------------------------------
    // Static defaults
    // ----------------------------------------------------------------------

    public const string DefaultTitle = "Untitled.md";

    public const string DefaultContent =
        "# New Markdown Document\n\nStart typing...\n";

    // ----------------------------------------------------------------------
    // Backing fields
    // ----------------------------------------------------------------------

    private string _documentId = Guid.NewGuid().ToString("N");
    private string _title = DefaultTitle;
    private string? _filePath;
    private string _content = DefaultContent;
    private bool _isDirty;
    private DateTimeOffset? _lastSavedUtc;
    private DateTimeOffset? _lastAutosavedUtc;

    // ----------------------------------------------------------------------
    // Properties
    // ----------------------------------------------------------------------

    /// <summary>
    /// Stable identifier for this document instance.
    /// Used to correlate autosave drafts to documents.
    /// </summary>
    public string DocumentId
    {
        get => _documentId;
        set => SetProperty(ref _documentId, value);
    }

    /// <summary>
    /// Display title, typically derived from FilePath when one exists.
    /// </summary>
    public string Title
    {
        get => _title;
        set
        {
            if (SetProperty(ref _title, value))
            {
                OnPropertyChanged(nameof(DisplayTitle));
            }
        }
    }

    /// <summary>
    /// Full path on disk, or null if the document has never been saved.
    /// </summary>
    public string? FilePath
    {
        get => _filePath;
        set
        {
            if (SetProperty(ref _filePath, value))
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    string fileName = Path.GetFileName(value);
                    if (!string.IsNullOrWhiteSpace(fileName))
                    {
                        Title = fileName;
                    }
                }

                OnPropertyChanged(nameof(HasFilePath));
                OnPropertyChanged(nameof(DisplayTitle));
            }
        }
    }

    /// <summary>
    /// Markdown source text.
    /// Setting this automatically marks the document as dirty.
    /// </summary>
    public string Content
    {
        get => _content;
        set
        {
            if (SetProperty(ref _content, value ?? string.Empty))
            {
                IsDirty = true;
            }
        }
    }

    /// <summary>
    /// True when there are unsaved changes vs. the last saved state.
    /// </summary>
    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            if (SetProperty(ref _isDirty, value))
            {
                OnPropertyChanged(nameof(DisplayTitle));
            }
        }
    }

    public DateTimeOffset? LastSavedUtc
    {
        get => _lastSavedUtc;
        set => SetProperty(ref _lastSavedUtc, value);
    }

    public DateTimeOffset? LastAutosavedUtc
    {
        get => _lastAutosavedUtc;
        set => SetProperty(ref _lastAutosavedUtc, value);
    }

    // ----------------------------------------------------------------------
    // Computed properties
    // ----------------------------------------------------------------------

    /// <summary>
    /// True if this document is backed by a file on disk.
    /// </summary>
    public bool HasFilePath => !string.IsNullOrWhiteSpace(_filePath);

    /// <summary>
    /// Title with a trailing "*" when there are unsaved changes.
    /// Useful for tab headers and window titles.
    /// </summary>
    public string DisplayTitle => _isDirty ? _title + " *" : _title;

    // ----------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------

    /// <summary>
    /// Mark the document as freshly saved (clears dirty + stamps time).
    /// </summary>
    public void MarkSaved(DateTimeOffset? savedAtUtc = null)
    {
        LastSavedUtc = savedAtUtc ?? DateTimeOffset.UtcNow;
        IsDirty = false;
    }

    /// <summary>
    /// Mark the document as freshly autosaved (does not clear dirty).
    /// </summary>
    public void MarkAutosaved(DateTimeOffset? autosavedAtUtc = null)
    {
        LastAutosavedUtc = autosavedAtUtc ?? DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Create a flat snapshot suitable for JSON serialization.
    /// </summary>
    public MarkdownDocumentSnapshot ToSnapshot()
    {
        return new MarkdownDocumentSnapshot
        {
            DocumentId = _documentId,
            Title = _title,
            FilePath = _filePath,
            Content = _content,
            IsDirty = _isDirty,
            LastSavedUtc = _lastSavedUtc,
            LastAutosavedUtc = _lastAutosavedUtc
        };
    }

    /// <summary>
    /// Construct a document from a flat snapshot.
    /// </summary>
    public static MarkdownDocument FromSnapshot(MarkdownDocumentSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return new MarkdownDocument();
        }

        return new MarkdownDocument
        {
            _documentId = string.IsNullOrWhiteSpace(snapshot.DocumentId)
                ? Guid.NewGuid().ToString("N")
                : snapshot.DocumentId,
            _title = string.IsNullOrWhiteSpace(snapshot.Title)
                ? DefaultTitle
                : snapshot.Title,
            _filePath = snapshot.FilePath,
            _content = snapshot.Content ?? string.Empty,
            _isDirty = snapshot.IsDirty,
            _lastSavedUtc = snapshot.LastSavedUtc,
            _lastAutosavedUtc = snapshot.LastAutosavedUtc
        };
    }

    // ----------------------------------------------------------------------
    // INotifyPropertyChanged
    // ----------------------------------------------------------------------

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetProperty<T>(
        ref T storage,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}