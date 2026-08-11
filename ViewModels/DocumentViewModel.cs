using System;
using System.ComponentModel;
using System.IO;

using MarkdownEditor.Models;

namespace MarkdownEditor.ViewModels;

/// <summary>
/// Per-tab ViewModel.
/// Wraps a single MarkdownDocument and exposes tab-friendly properties.
/// </summary>
public sealed class DocumentViewModel : BindableBase
{
    // ----------------------------------------------------------------------
    // Backing state
    // ----------------------------------------------------------------------

    private readonly MarkdownDocument _document;
    private bool _isActive;
    private string _statusMessage = "Ready";

    // ----------------------------------------------------------------------
    // Construction
    // ----------------------------------------------------------------------

    public DocumentViewModel()
        : this(new MarkdownDocument())
    {
    }

    public DocumentViewModel(MarkdownDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _document.PropertyChanged += OnDocumentPropertyChanged;
    }

    // ----------------------------------------------------------------------
    // Public properties
    // ----------------------------------------------------------------------

    /// <summary>
    /// Underlying document model.
    /// </summary>
    public MarkdownDocument Document => _document;

    /// <summary>
    /// Stable identifier for autosave and session tracking.
    /// </summary>
    public string DocumentId => _document.DocumentId;

    /// <summary>
    /// Bindable Markdown content for the editor.
    /// </summary>
    public string MarkdownText
    {
        get => _document.Content;
        set
        {
            if (_document.Content == value)
            {
                return;
            }

            _document.Content = value ?? string.Empty;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Display title (filename only, no path).
    /// </summary>
    public string Title => _document.Title;

    /// <summary>
    /// Title with "*" suffix when there are unsaved changes.
    /// Used by the TabView header.
    /// </summary>
    public string DisplayTitle => _document.DisplayTitle;

    /// <summary>
    /// Full file path or null if the document has never been saved.
    /// </summary>
    public string? FilePath => _document.FilePath;

    /// <summary>
    /// Hover tooltip text - full path or "Not saved".
    /// </summary>
    public string Tooltip
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_document.FilePath))
            {
                return "Not saved";
            }

            return _document.FilePath!;
        }
    }

    /// <summary>
    /// True when there are unsaved changes vs. the last saved state.
    /// </summary>
    public bool IsDirty => _document.IsDirty;

    /// <summary>
    /// True when this tab is the active one.
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    /// <summary>
    /// Per-tab status message (e.g., "Unsaved changes", "Saved").
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    // ----------------------------------------------------------------------
    // Document operations
    // ----------------------------------------------------------------------

    /// <summary>
    /// Mark the document as freshly saved.
    /// </summary>
    public void MarkSaved()
    {
        _document.MarkSaved();
    }

    /// <summary>
    /// Mark the document as freshly autosaved.
    /// </summary>
    public void MarkAutosaved()
    {
        _document.MarkAutosaved();
    }

    /// <summary>
    /// Returns true if this tab represents the given file path
    /// (case-insensitive match).
    /// </summary>
    public bool MatchesFilePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || string.IsNullOrWhiteSpace(_document.FilePath))
        {
            return false;
        }

        return string.Equals(
            _document.FilePath,
            path,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Update the file path (e.g., after Save As).
    /// Also updates the Title to match the filename.
    /// </summary>
    public void SetFilePath(string path)
    {
        _document.FilePath = path;

        if (!string.IsNullOrWhiteSpace(path))
        {
            string fileName = Path.GetFileName(path);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                _document.Title = fileName;
            }
        }
    }

    /// <summary>
    /// Replace the content of the document (used by Open, restore, templates).
    /// </summary>
    public void SetContent(string content)
    {
        _document.Content = content ?? string.Empty;
    }

    // ----------------------------------------------------------------------
    // Underlying document change handling
    // ----------------------------------------------------------------------

    private void OnDocumentPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MarkdownDocument.Title):
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(DisplayTitle));
                break;

            case nameof(MarkdownDocument.DisplayTitle):
                OnPropertyChanged(nameof(DisplayTitle));
                break;

            case nameof(MarkdownDocument.IsDirty):
                OnPropertyChanged(nameof(IsDirty));
                OnPropertyChanged(nameof(DisplayTitle));
                break;

            case nameof(MarkdownDocument.FilePath):
                OnPropertyChanged(nameof(FilePath));
                OnPropertyChanged(nameof(Tooltip));
                break;

            case nameof(MarkdownDocument.Content):
                OnPropertyChanged(nameof(MarkdownText));
                break;
        }
    }
}