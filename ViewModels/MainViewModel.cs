using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

using MarkdownEditor.Helpers;
using MarkdownEditor.Models;
using MarkdownEditor.Services;

namespace MarkdownEditor.ViewModels;

/// <summary>
/// Shell ViewModel for the main window.
/// Holds the collection of open tabs and forwards "active tab" properties
/// to keep existing bindings working without changes to the View.
/// </summary>
public sealed class MainViewModel : BindableBase
{
    // ----------------------------------------------------------------------
    // Dependencies
    // ----------------------------------------------------------------------

    private readonly ILoggingService? _logger;
    private readonly ISettingsService? _settings;
    private readonly IAutosaveService? _autosave;
    private readonly IMarkdownFileService? _files;

    // ----------------------------------------------------------------------
    // State
    // ----------------------------------------------------------------------

    private DocumentViewModel? _activeTab;
    private string _statusMessage = "Ready";
    private bool _isEditorVisible = true;
    private bool _isPreviewVisible = true;
    private bool _isInitialized;
    private bool _isRestoring;
    private int _untitledCounter;

    private IntPtr _ownerHandle = IntPtr.Zero;

    // ----------------------------------------------------------------------
    // Tab collection
    // ----------------------------------------------------------------------

    public ObservableCollection<DocumentViewModel> Tabs { get; }

    public DocumentViewModel? ActiveTab
    {
        get => _activeTab;
        set
        {
            DocumentViewModel? previous = _activeTab;

            if (!SetProperty(ref _activeTab, value))
            {
                return;
            }

            if (previous != null)
            {
                previous.IsActive = false;
                previous.PropertyChanged -= OnActiveTabPropertyChanged;
            }

            if (_activeTab != null)
            {
                _activeTab.IsActive = true;
                _activeTab.PropertyChanged += OnActiveTabPropertyChanged;
            }

            OnPropertiesChanged(
                nameof(MarkdownText),
                nameof(Title),
                nameof(WindowTitle),
                nameof(IsDirty),
                nameof(Document),
                nameof(HasActiveTab));
        }
    }

    public bool HasActiveTab => _activeTab != null;

    // ----------------------------------------------------------------------
    // Forwarded properties (kept for backwards compatibility with the View)
    // ----------------------------------------------------------------------

    /// <summary>
    /// Underlying document model of the active tab.
    /// </summary>
    public MarkdownDocument? Document => _activeTab?.Document;

    public string MarkdownText
    {
        get => _activeTab?.MarkdownText ?? string.Empty;
        set
        {
            if (_activeTab == null)
            {
                return;
            }

            if (_activeTab.MarkdownText == value)
            {
                return;
            }

            _activeTab.MarkdownText = value ?? string.Empty;

            OnPropertyChanged();
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(IsDirty));

            if (!_isRestoring)
            {
                StatusMessage = "Unsaved changes";
                _autosave?.ScheduleAutosave(_activeTab.Document);
            }
        }
    }

    public string Title => _activeTab?.Title ?? "Untitled.md";

    public string WindowTitle
    {
        get
        {
            string display = _activeTab?.DisplayTitle ?? "Untitled.md";
            return $"{display} - Markdown Editor";
        }
    }

    public bool IsDirty => _activeTab?.IsDirty ?? false;

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsEditorVisible
    {
        get => _isEditorVisible;
        set => SetProperty(ref _isEditorVisible, value);
    }

    public bool IsPreviewVisible
    {
        get => _isPreviewVisible;
        set => SetProperty(ref _isPreviewVisible, value);
    }

    // ----------------------------------------------------------------------
    // Commands
    // ----------------------------------------------------------------------

    public ICommand NewCommand { get; }
    public ICommand OpenCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAsCommand { get; }
    public ICommand ExportPdfCommand { get; }
    public ICommand ToggleEditorCommand { get; }
    public ICommand TogglePreviewCommand { get; }

    public ICommand CloseTabCommand { get; }
    public ICommand CloseOthersCommand { get; }
    public ICommand CloseAllCommand { get; }

    public ICommand BoldCommand { get; }
    public ICommand ItalicCommand { get; }
    public ICommand HeadingCommand { get; }
    public ICommand BulletCommand { get; }
    public ICommand CodeCommand { get; }
    public ICommand LinkCommand { get; }

    // ----------------------------------------------------------------------
    // Events
    // ----------------------------------------------------------------------

    public event EventHandler<string>? FormattingRequested;
    public event EventHandler? ExportPdfRequested;

    // ----------------------------------------------------------------------
    // Construction
    // ----------------------------------------------------------------------

    public MainViewModel()
        : this(
            App.Logger,
            App.Settings,
            App.Autosave,
            App.Files)
    {
    }

    public MainViewModel(
        ILoggingService? logger,
        ISettingsService? settings,
        IAutosaveService? autosave,
        IMarkdownFileService? files)
    {
        _logger = logger;
        _settings = settings;
        _autosave = autosave;
        _files = files;

        Tabs = new ObservableCollection<DocumentViewModel>();

        if (_settings != null)
        {
            _isEditorVisible = _settings.Current.ShowEditorOnLaunch;
            _isPreviewVisible = _settings.Current.ShowPreviewOnLaunch;
        }

        NewCommand = new RelayCommand(() => AddNewTab(activate: true));
        OpenCommand = new AsyncRelayCommand(OpenAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        SaveAsCommand = new AsyncRelayCommand(SaveAsAsync);
        ExportPdfCommand = new RelayCommand(RaiseExportPdfRequested);
        ToggleEditorCommand = new RelayCommand(ToggleEditor);
        TogglePreviewCommand = new RelayCommand(TogglePreview);

        CloseTabCommand = new RelayCommand<DocumentViewModel>(CloseTab);
        CloseOthersCommand = new RelayCommand<DocumentViewModel>(CloseOthers);
        CloseAllCommand = new RelayCommand(CloseAll);

        BoldCommand = new RelayCommand(() => RaiseFormattingRequested("bold"));
        ItalicCommand = new RelayCommand(() => RaiseFormattingRequested("italic"));
        HeadingCommand = new RelayCommand(() => RaiseFormattingRequested("heading"));
        BulletCommand = new RelayCommand(() => RaiseFormattingRequested("bullet"));
        CodeCommand = new RelayCommand(() => RaiseFormattingRequested("code"));
        LinkCommand = new RelayCommand(() => RaiseFormattingRequested("link"));
    }

    // ----------------------------------------------------------------------
    // View interaction surface
    // ----------------------------------------------------------------------

    public void SetOwnerHandle(IntPtr handle)
    {
        _ownerHandle = handle;
    }

    private void RaiseFormattingRequested(string action)
    {
        FormattingRequested?.Invoke(this, action);
    }

    private void RaiseExportPdfRequested()
    {
        ExportPdfRequested?.Invoke(this, EventArgs.Empty);
    }

    // ----------------------------------------------------------------------
    // Active tab change notifications
    // ----------------------------------------------------------------------

    private void OnActiveTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(DocumentViewModel.Title):
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(WindowTitle));
                break;

            case nameof(DocumentViewModel.DisplayTitle):
                OnPropertyChanged(nameof(WindowTitle));
                break;

            case nameof(DocumentViewModel.IsDirty):
                OnPropertyChanged(nameof(IsDirty));
                OnPropertyChanged(nameof(WindowTitle));
                break;

            case nameof(DocumentViewModel.MarkdownText):
                OnPropertyChanged(nameof(MarkdownText));
                break;
        }
    }

    // ----------------------------------------------------------------------
    // Tab management
    // ----------------------------------------------------------------------

    public DocumentViewModel AddNewTab(bool activate = true)
    {
        _untitledCounter++;

        MarkdownDocument doc = new MarkdownDocument();
        doc.Title = $"Untitled {_untitledCounter}.md";

        DocumentViewModel tab = new DocumentViewModel(doc);
        Tabs.Add(tab);

        if (activate || ActiveTab == null)
        {
            ActiveTab = tab;
        }

        StatusMessage = "New document";

        return tab;
    }

    public DocumentViewModel? FindTabByPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return Tabs.FirstOrDefault(t => t.MatchesFilePath(path));
    }

    public void CloseTab(DocumentViewModel? tab)
    {
        if (tab == null || !Tabs.Contains(tab))
        {
            return;
        }

        bool wasActive = ReferenceEquals(tab, _activeTab);
        int oldIndex = Tabs.IndexOf(tab);

        // Best-effort: clear autosave draft for the closed tab.
        try
        {
            _ = _autosave?.ClearDraftAsync(tab.Document);
        }
        catch
        {
            // Ignore; closing should not fail because of autosave cleanup.
        }

        Tabs.Remove(tab);

        if (Tabs.Count == 0)
        {
            // Never leave the user with zero tabs.
            AddNewTab(activate: true);
            return;
        }

        if (wasActive)
        {
            int newIndex = Math.Min(oldIndex, Tabs.Count - 1);
            ActiveTab = Tabs[newIndex];
        }
    }

    public void CloseOthers(DocumentViewModel? keepTab)
    {
        if (keepTab == null)
        {
            return;
        }

        // Snapshot so we can iterate while modifying.
        List<DocumentViewModel> toClose = Tabs
            .Where(t => !ReferenceEquals(t, keepTab))
            .ToList();

        foreach (DocumentViewModel t in toClose)
        {
            CloseTab(t);
        }

        ActiveTab = keepTab;
    }

    public void CloseAll()
    {
        List<DocumentViewModel> all = Tabs.ToList();

        foreach (DocumentViewModel t in all)
        {
            try
            {
                _ = _autosave?.ClearDraftAsync(t.Document);
            }
            catch
            {
                // Ignore.
            }
        }

        Tabs.Clear();

        AddNewTab(activate: true);
    }

    // ----------------------------------------------------------------------
    // Lifecycle
    // ----------------------------------------------------------------------

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;

        bool restoreEnabled = _settings?.Current.RestoreSessionOnLaunch ?? true;

        if (!restoreEnabled || _autosave == null)
        {
            AddNewTab(activate: true);
            StatusMessage = "Ready";
            return;
        }

        try
        {
            IReadOnlyList<MarkdownDocumentSnapshot> drafts =
                await _autosave.LoadDraftsAsync().ConfigureAwait(true);

            if (drafts == null || drafts.Count == 0)
            {
                AddNewTab(activate: true);
                StatusMessage = "Ready";
                return;
            }

            _isRestoring = true;

            foreach (MarkdownDocumentSnapshot snap in drafts)
            {
                MarkdownDocument doc = MarkdownDocument.FromSnapshot(snap);
                DocumentViewModel tab = new DocumentViewModel(doc);
                Tabs.Add(tab);
            }

            _isRestoring = false;

            ActiveTab = Tabs.FirstOrDefault();

            StatusMessage = Tabs.Count == 1
                ? "Draft restored from autosave"
                : $"Restored {Tabs.Count} drafts from autosave";

            _logger?.Info($"Restored {Tabs.Count} draft(s) from autosave.", "ViewModel");
        }
        catch (Exception ex)
        {
            _logger?.Error("Failed to restore drafts on launch.", ex, "ViewModel");
            StatusMessage = "Failed to restore previous session";

            if (Tabs.Count == 0)
            {
                AddNewTab(activate: true);
            }
        }
    }

    public async Task ForceAutosaveAsync()
    {
        if (_autosave == null)
        {
            return;
        }

        List<DocumentViewModel> snapshot = Tabs.ToList();

        foreach (DocumentViewModel tab in snapshot)
        {
            try
            {
                await _autosave.ForceSaveAsync(tab.Document).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger?.Error(
                    $"Force autosave failed for tab '{tab.Title}'.",
                    ex,
                    "ViewModel");
            }
        }

        // Update status silently - if the window is shutting down, ignore any
        // binding update failures.
        try
        {
            StatusMessage = "Autosaved";
        }
        catch
        {
            // The window is closing; ignore.
        }
    }

    // ----------------------------------------------------------------------
    // Command implementations
    // ----------------------------------------------------------------------

    private async Task OpenAsync()
    {
        if (_files == null)
        {
            StatusMessage = "File service unavailable";
            return;
        }

        try
        {
            OpenFileResult? result =
                await _files.OpenAsync(_ownerHandle).ConfigureAwait(true);

            if (result == null)
            {
                StatusMessage = "Open canceled";
                return;
            }

            // If this file is already open in a tab, switch to it instead.
            DocumentViewModel? existing = FindTabByPath(result.FilePath);
            if (existing != null)
            {
                ActiveTab = existing;
                StatusMessage = $"Switched to {existing.Title}";
                return;
            }

            // Otherwise create a new tab.
            MarkdownDocument doc = new MarkdownDocument();

            _isRestoring = true;
            doc.FilePath = result.FilePath;
            doc.Content = result.Content;
            doc.MarkSaved();
            _isRestoring = false;

            DocumentViewModel tab = new DocumentViewModel(doc);
            Tabs.Add(tab);
            ActiveTab = tab;

            StatusMessage = $"Opened {tab.Title}";

            if (_autosave != null)
            {
                await _autosave.ForceSaveAsync(doc).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            _logger?.Error("OpenAsync failed.", ex, "ViewModel");
            StatusMessage = "Failed to open file";
        }
    }

    private async Task SaveAsync()
    {
        if (_files == null || _activeTab == null)
        {
            StatusMessage = "Nothing to save";
            return;
        }

        try
        {
            MarkdownDocument doc = _activeTab.Document;

            if (string.IsNullOrWhiteSpace(doc.FilePath))
            {
                string? newPath = await _files.SaveAsAsync(doc, _ownerHandle)
                    .ConfigureAwait(true);

                if (string.IsNullOrWhiteSpace(newPath))
                {
                    StatusMessage = "Save canceled";
                    return;
                }

                await AfterSaveAsync(_activeTab, doc.FilePath ?? newPath)
                    .ConfigureAwait(true);
                return;
            }

            bool ok = await _files.SaveAsync(doc, _ownerHandle).ConfigureAwait(true);

            if (!ok)
            {
                StatusMessage = "Save failed";
                return;
            }

            await AfterSaveAsync(_activeTab, doc.FilePath!).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger?.Error("SaveAsync failed.", ex, "ViewModel");
            StatusMessage = "Failed to save file";
        }
    }

    private async Task SaveAsAsync()
    {
        if (_files == null || _activeTab == null)
        {
            StatusMessage = "Nothing to save";
            return;
        }

        try
        {
            string? newPath = await _files
                .SaveAsAsync(_activeTab.Document, _ownerHandle)
                .ConfigureAwait(true);

            if (string.IsNullOrWhiteSpace(newPath))
            {
                StatusMessage = "Save canceled";
                return;
            }

            await AfterSaveAsync(
                _activeTab,
                _activeTab.Document.FilePath ?? newPath)
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger?.Error("SaveAsAsync failed.", ex, "ViewModel");
            StatusMessage = "Failed to save file";
        }
    }

    private async Task AfterSaveAsync(DocumentViewModel tab, string savedPath)
    {
        StatusMessage = $"Saved {Path.GetFileName(savedPath)}";

        OnPropertiesChanged(
            nameof(Title),
            nameof(WindowTitle),
            nameof(IsDirty));

        if (_autosave != null)
        {
            await _autosave.ClearDraftAsync(tab.Document).ConfigureAwait(true);
        }

        _logger?.Info($"Document saved: {savedPath}", "ViewModel");
    }

    public async Task OpenRecentAsync(string path)
    {
        if (_files == null || string.IsNullOrWhiteSpace(path))
        {
            StatusMessage = "File service unavailable";
            return;
        }

        try
        {
            if (!File.Exists(path))
            {
                StatusMessage = $"File not found: {path}";

                if (_settings != null)
                {
                    _settings.Current.RecentFiles.Remove(path);
                    _settings.Save();
                }

                _logger?.Warn($"Recent file no longer exists: {path}", "ViewModel");
                return;
            }

            // Switch to existing tab if already open.
            DocumentViewModel? existing = FindTabByPath(path);
            if (existing != null)
            {
                ActiveTab = existing;
                StatusMessage = $"Switched to {existing.Title}";
                return;
            }

            string content = await File.ReadAllTextAsync(path).ConfigureAwait(true);

            _isRestoring = true;

            MarkdownDocument doc = new MarkdownDocument();
            doc.FilePath = path;
            doc.Content = content;
            doc.MarkSaved();

            _isRestoring = false;

            DocumentViewModel tab = new DocumentViewModel(doc);
            Tabs.Add(tab);
            ActiveTab = tab;

            StatusMessage = $"Opened {tab.Title}";

            if (_settings != null)
            {
                _settings.AddRecentFile(path);
                _settings.Save();
            }

            if (_autosave != null)
            {
                await _autosave.ForceSaveAsync(doc).ConfigureAwait(true);
            }

            _logger?.Info($"Opened recent file: {path}", "ViewModel");
        }
        catch (Exception ex)
        {
            _logger?.Error($"OpenRecent failed: {path}", ex, "ViewModel");
            StatusMessage = "Failed to open file";
        }
    }

    public async Task NewFromTemplateAsync(string templatePath, string templateName)
    {
        if (string.IsNullOrWhiteSpace(templatePath))
        {
            StatusMessage = "Template path missing";
            return;
        }

        try
        {
            ITemplateService? templateService = App.Templates;

            if (templateService == null)
            {
                StatusMessage = "Template service unavailable";
                return;
            }

            string content = await templateService.LoadTemplateAsync(templatePath)
                .ConfigureAwait(true);

            if (string.IsNullOrWhiteSpace(content))
            {
                StatusMessage = $"Template '{templateName}' is empty or unreadable";
                return;
            }

            _isRestoring = true;

            MarkdownDocument doc = new MarkdownDocument();

            string safeName = string.IsNullOrWhiteSpace(templateName)
                ? "Untitled.md"
                : templateName + ".md";

            doc.Title = safeName;
            doc.Content = content;

            // Document is intentionally NOT marked saved - it's a new unsaved doc.

            DocumentViewModel tab = new DocumentViewModel(doc);
            Tabs.Add(tab);
            ActiveTab = tab;

            _isRestoring = false;

            StatusMessage = $"Created from template: {templateName}";

            _logger?.Info(
                $"Created new document from template: {templateName}",
                "ViewModel");

            if (_autosave != null)
            {
                _autosave.ScheduleAutosave(doc);
            }
        }
        catch (Exception ex)
        {
            _logger?.Error(
                $"NewFromTemplate failed for: {templatePath}",
                ex,
                "ViewModel");

            StatusMessage = "Failed to load template";
        }
    }

    // ----------------------------------------------------------------------
    // View toggles
    // ----------------------------------------------------------------------

    private void ToggleEditor()
    {
        IsEditorVisible = !IsEditorVisible;

        if (!IsEditorVisible && !IsPreviewVisible)
        {
            IsPreviewVisible = true;
        }

        StatusMessage = IsEditorVisible ? "Editor visible" : "Editor hidden";
    }

    private void TogglePreview()
    {
        IsPreviewVisible = !IsPreviewVisible;

        if (!IsPreviewVisible && !IsEditorVisible)
        {
            IsEditorVisible = true;
        }

        StatusMessage = IsPreviewVisible ? "Preview visible" : "Preview hidden";
    }
}