using Markdig;
using MarkdownEditor.Helpers;
using MarkdownEditor.Models;
using MarkdownEditor.Services;
using MarkdownEditor.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using WinRT.Interop;

namespace MarkdownEditor
{
    public sealed partial class MainWindow : Window
    {
        // ------------------------------------------------------------------
        // State
        // ------------------------------------------------------------------

        private readonly MarkdownPipeline _markdownPipeline;

        // ------------------------------------------------------------------
        // Properties
        // ------------------------------------------------------------------

        private string _currentTheme = "System";
        private bool _isFindPanelVisible;
        private int _lastMatchCount;
        private int _lastMatchIndex;
        private string _lastSearchTerm = string.Empty;
        private bool _lastMatchCase;
        private bool _lastMatchWholeWord;
        private bool _suppressFindSearch;

        // Per-tab editor and preview tracking.
        private readonly Dictionary<DocumentViewModel, TextBox> _editorByTab = new();
        private readonly Dictionary<DocumentViewModel, WebView2> _previewByTab = new();

        // These fields keep existing code working - they point to the active tab's
        // editor and preview. They get updated when the active tab changes.
        private TextBox? EditorTextBox;
        private WebView2? PreviewWebView;
        public MainViewModel ViewModel { get; }

        // ------------------------------------------------------------------
        // Construction
        // ------------------------------------------------------------------

        public MainWindow()
        {
            this.InitializeComponent();

            Title = "Markdown Editor";

            ViewModel = new MainViewModel();
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            ViewModel.FormattingRequested += ViewModel_FormattingRequested;
            ViewModel.ExportPdfRequested += ViewModel_ExportPdfRequested;

            _markdownPipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .Build();

            RootGrid.Loaded += RootGrid_Loaded;
            this.Closed += MainWindow_Closed;

            // Set the Windows shell icon
            TrySetAppWindowIcon();

        }

        private void ApplyWindowSettings()
        {
            try
            {
                if (App.Settings == null)
                {
                    return;
                }

                var current = App.Settings.Current;

                IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                Microsoft.UI.WindowId windowId =
                    Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);

                Microsoft.UI.Windowing.AppWindow appWindow =
                    Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

                int width = current.WindowWidth > 0 ? current.WindowWidth : 1280;
                int height = current.WindowHeight > 0 ? current.WindowHeight : 800;

                bool hasValidPosition =
                    current.WindowLeft >= 0 && current.WindowTop >= 0;

                if (hasValidPosition)
                {
                    appWindow.MoveAndResize(
                        new Windows.Graphics.RectInt32(
                            current.WindowLeft,
                            current.WindowTop,
                            width,
                            height));
                }
                else
                {
                    appWindow.Resize(
                        new Windows.Graphics.SizeInt32(width, height));
                }

                if (current.WindowMaximized
                    && appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
                {
                    presenter.Maximize();
                }

                App.Logger?.Info(
                    $"Window restored to {width}x{height} at ({current.WindowLeft},{current.WindowTop}) " +
                    $"maximized={current.WindowMaximized}",
                    "MainWindow");
            }
            catch (Exception ex)
            {
                App.Logger?.Error("Failed to apply window settings.", ex, "MainWindow");
            }
        }

        private void SaveWindowSettings()
        {
            try
            {
                if (App.Settings == null)
                {
                    return;
                }

                IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                Microsoft.UI.WindowId windowId =
                    Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);

                Microsoft.UI.Windowing.AppWindow appWindow =
                    Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

                bool isMaximized = false;

                if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
                {
                    isMaximized =
                        presenter.State == Microsoft.UI.Windowing.OverlappedPresenterState.Maximized;
                }

                var settings = App.Settings.Current;
                settings.WindowMaximized = isMaximized;

                if (!isMaximized)
                {
                    settings.WindowWidth = appWindow.Size.Width;
                    settings.WindowHeight = appWindow.Size.Height;
                    settings.WindowLeft = appWindow.Position.X;
                    settings.WindowTop = appWindow.Position.Y;
                }

                App.Settings.Save();

                App.Logger?.Info(
                    $"Window state saved: {settings.WindowWidth}x{settings.WindowHeight} " +
                    $"at ({settings.WindowLeft},{settings.WindowTop}) maximized={settings.WindowMaximized}",
                    "MainWindow");
            }
            catch (Exception ex)
            {
                App.Logger?.Error("Failed to save window settings.", ex, "MainWindow");
            }
        }

        // Icon Settings
        private void TrySetAppWindowIcon()
        {
            try
            {
                IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

                Microsoft.UI.WindowId windowId =
                    Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);

                Microsoft.UI.Windowing.AppWindow appWindow =
                    Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

                string iconPath = System.IO.Path.Combine(
                    AppContext.BaseDirectory,
                    "Assets",
                    "AppIcon.ico");

                if (System.IO.File.Exists(iconPath))
                {
                    appWindow.SetIcon(iconPath);
                }
                else
                {
                    App.Logger?.Warn(
                        $"App icon not found at: {iconPath}",
                        "MainWindow");
                }
            }
            catch (Exception ex)
            {
                App.Logger?.Error(
                    "Failed to set app window icon.",
                    ex,
                    "MainWindow");
            }
        }


        private void ApplyThemeFromSettings()
        {
            try
            {
                string theme = App.Settings?.Current.Theme ?? "System";
                ApplyTheme(theme, persist: false);
            }
            catch (Exception ex)
            {
                App.Logger?.Error("Failed to apply initial theme.", ex, "MainWindow");
            }
        }

        private void ApplyTheme(string theme, bool persist)
        {
            try
            {
                _currentTheme = theme ?? "System";

                if (RootGrid is FrameworkElement root)
                {
                    root.RequestedTheme = _currentTheme switch
                    {
                        "Light" => ElementTheme.Light,
                        "Dark" => ElementTheme.Dark,
                        _ => ElementTheme.Default
                    };
                }

                RefreshAllPreviews();

                if (persist && App.Settings != null)
                {
                    App.Settings.Current.Theme = _currentTheme;
                    App.Settings.Save();
                }

                App.Logger?.Info($"Theme set to {_currentTheme}.", "MainWindow");
            }
            catch (Exception ex)
            {
                App.Logger?.Error($"Failed to apply theme: {theme}", ex, "MainWindow");
            }
        }

        private void RefreshAllPreviews()
        {
            // Refresh every tab's preview so a theme change propagates instantly
            // to all open tabs, not just the active one.
            foreach (KeyValuePair<DocumentViewModel, WebView2> kvp in _previewByTab)
            {
                try
                {
                    DocumentViewModel vm = kvp.Key;
                    WebView2 wv = kvp.Value;

                    if (wv?.CoreWebView2 != null)
                    {
                        RenderPreviewForTab(wv, vm.MarkdownText);
                    }
                }
                catch
                {
                    // Never let a single-tab refresh break the whole loop.
                }
            }
        }

        private void ThemeSystemItem_Click(object sender, RoutedEventArgs e)
        {
            ApplyTheme("System", persist: true);
        }

        private void ThemeLightItem_Click(object sender, RoutedEventArgs e)
        {
            ApplyTheme("Light", persist: true);
        }

        private void ThemeDarkItem_Click(object sender, RoutedEventArgs e)
        {
            ApplyTheme("Dark", persist: true);
        }

        private bool IsEffectiveDarkTheme()
        {
            if (_currentTheme == "Light")
            {
                return false;
            }

            if (_currentTheme == "Dark")
            {
                return true;
            }

            if (RootGrid is FrameworkElement root)
            {
                return root.ActualTheme == ElementTheme.Dark;
            }

            return false;
        }

        // ------------------------------------------------------------------
        // Loaded
        // ------------------------------------------------------------------

        private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Restore window size, position, and maximize state.
                ApplyWindowSettings();

                // Apply persisted theme to the whole visual tree.
                ApplyThemeFromSettings();

                // Provide the window handle to the ViewModel for file dialogs.
                IntPtr hwnd = WindowNative.GetWindowHandle(this);
                ViewModel.SetOwnerHandle(hwnd);

                // Show portable mode badge in the status bar.
                UpdateEnvironmentBadge();

                // Restore drafts as tabs, or open a new Untitled tab if none exist.
                await ViewModel.InitializeAsync();

                // The per-tab editor / preview instances load themselves via
                // EditorInstance_Loaded and PreviewInstance_Loaded. The editor for the
                // active tab focuses itself when it loads. Nothing else to do here.
            }
            catch (Exception ex)
            {
                App.Logger?.Error("MainWindow load failed.", ex, "MainWindow");
            }
        }

        // ------------------------------------------------------------------
        // Closing
        // ------------------------------------------------------------------

        private async void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            try
            {
                // Save window size and position before shutting down
                SaveWindowSettings();


                await ViewModel.ForceAutosaveAsync();
            }
            catch (Exception ex)
            {
                App.Logger?.Error("Force autosave on close failed.", ex, "MainWindow");
            }
        }

        // ----------------------------------------------------------------------
        // Markdown Shortcuts - List auto-continuation
        // ----------------------------------------------------------------------

        private void EditorTextBox_KeyDown(
            object sender,
            Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            Windows.UI.Core.CoreVirtualKeyStates shiftState =
                Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(
                    Windows.System.VirtualKey.Shift);

            Windows.UI.Core.CoreVirtualKeyStates ctrlState =
                Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(
                    Windows.System.VirtualKey.Control);

            bool shiftDown =
                (shiftState & Windows.UI.Core.CoreVirtualKeyStates.Down)
                == Windows.UI.Core.CoreVirtualKeyStates.Down;

            bool ctrlDown =
                (ctrlState & Windows.UI.Core.CoreVirtualKeyStates.Down)
                == Windows.UI.Core.CoreVirtualKeyStates.Down;

            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                // Plain Enter only - no Shift or Ctrl.
                if (shiftDown || ctrlDown)
                {
                    return;
                }

                try
                {
                    TryAutoContinueList(e);
                }
                catch (Exception ex)
                {
                    App.Logger?.Warn($"Auto-continue list failed: {ex.Message}", "Markdown");
                }
            }
            else if (e.Key == Windows.System.VirtualKey.Tab)
            {
                // Tab and Shift+Tab only - never Ctrl+Tab.
                if (ctrlDown)
                {
                    return;
                }

                try
                {
                    TryIndentList(e, outdent: shiftDown);
                }
                catch (Exception ex)
                {
                    App.Logger?.Warn($"List indent failed: {ex.Message}", "Markdown");
                }
            }
        }

        private void TryAutoContinueList(Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            string text = EditorTextBox.Text ?? string.Empty;
            int caret = EditorTextBox.SelectionStart;
            int selLength = EditorTextBox.SelectionLength;

            // Don't intercept when there is a multi-character selection.
            if (selLength > 0)
            {
                return;
            }

            // The TextBox has already inserted \r\n. Walk backward over the newline
            // characters to find the end of the previous line.
            int searchPos = caret - 1;
            while (searchPos >= 0
                && (text[searchPos] == '\r' || text[searchPos] == '\n'))
            {
                searchPos--;
            }

            if (searchPos < 0)
            {
                return;
            }

            int prevLineEnd = searchPos + 1;

            // Find start of the previous line.
            int prevLineStart = prevLineEnd;
            while (prevLineStart > 0
                && text[prevLineStart - 1] != '\n'
                && text[prevLineStart - 1] != '\r')
            {
                prevLineStart--;
            }

            string prevLine = text.Substring(prevLineStart, prevLineEnd - prevLineStart);

            ListLineMatch? match = MatchListLine(prevLine);
            if (match == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(match.Content))
            {
                // Empty list item: remove the marker line AND the newline we just typed.
                int deleteStart = prevLineStart;
                int deleteLength = caret - prevLineStart;

                EditorTextBox.SelectionStart = deleteStart;
                EditorTextBox.SelectionLength = deleteLength;
                EditorTextBox.SelectedText = string.Empty;

                EditorTextBox.SelectionStart = deleteStart;
                EditorTextBox.SelectionLength = 0;
                return;
            }

            // Build the continuation marker.
            string nextMarker;
            if (match.IsOrdered)
            {
                nextMarker = (match.Number + 1).ToString() + ". ";
            }
            else
            {
                nextMarker = match.Marker + " ";
            }

            string insertion = match.Indent + nextMarker;

            // Insert at the current caret (which is right after the newline).
            EditorTextBox.SelectedText = insertion;

            int newCaret = caret + insertion.Length;
            EditorTextBox.SelectionStart = newCaret;
            EditorTextBox.SelectionLength = 0;
        }

        private void TryIndentList(
    Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e,
    bool outdent)
        {
            const string IndentUnit = "  ";  // 2 spaces

            string text = EditorTextBox.Text ?? string.Empty;
            int caret = EditorTextBox.SelectionStart;
            int selLength = EditorTextBox.SelectionLength;

            // For now, single-line behavior only. Multi-line indent is a future enhancement.
            if (selLength > 0)
            {
                return;
            }

            // Find current line bounds.
            int lineStart = caret;
            while (lineStart > 0
                && text[lineStart - 1] != '\n'
                && text[lineStart - 1] != '\r')
            {
                lineStart--;
            }

            int lineEnd = caret;
            while (lineEnd < text.Length
                && text[lineEnd] != '\n'
                && text[lineEnd] != '\r')
            {
                lineEnd++;
            }

            string currentLine = text.Substring(lineStart, lineEnd - lineStart);

            ListLineMatch? match = MatchListLine(currentLine);
            if (match == null)
            {
                // Not a list line - let Tab behave normally.
                return;
            }

            int currentIndentLen = match.Indent.Length;
            string newLine;
            int caretShift;

            if (outdent)
            {
                if (currentIndentLen == 0)
                {
                    // Already at outermost level - nothing to do.
                    e.Handled = true;
                    return;
                }

                int removeCount = Math.Min(IndentUnit.Length, currentIndentLen);
                newLine = currentLine.Substring(removeCount);
                caretShift = -removeCount;
            }
            else
            {
                newLine = IndentUnit + currentLine;
                caretShift = IndentUnit.Length;
            }

            // Replace the entire current line.
            EditorTextBox.SelectionStart = lineStart;
            EditorTextBox.SelectionLength = lineEnd - lineStart;
            EditorTextBox.SelectedText = newLine;

            // Restore caret to its relative position within the (now shifted) line.
            int newCaret = caret + caretShift;
            if (newCaret < lineStart)
            {
                newCaret = lineStart;
            }

            EditorTextBox.SelectionStart = newCaret;
            EditorTextBox.SelectionLength = 0;

            e.Handled = true;
        }

        // ----------------------------------------------------------------------
        // Per-tab editor lifecycle
        // ----------------------------------------------------------------------

        private void EditorInstance_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox tb)
            {
                return;
            }

            if (tb.DataContext is not DocumentViewModel vm)
            {
                return;
            }

            _editorByTab[vm] = tb;

            // Hook KeyDown via AddHandler so we receive Enter / Tab even after the
            // TextBox marks them as handled (needed for list auto-continuation and
            // Tab indent).
            tb.AddHandler(
                UIElement.KeyDownEvent,
                new Microsoft.UI.Xaml.Input.KeyEventHandler(EditorTextBox_KeyDown),
                handledEventsToo: true);

            if (ReferenceEquals(vm, ViewModel.ActiveTab))
            {
                EditorTextBox = tb;
                tb.Focus(FocusState.Programmatic);
            }
        }

        private void EditorInstance_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox tb)
            {
                return;
            }

            if (tb.DataContext is not DocumentViewModel vm)
            {
                return;
            }

            if (_editorByTab.TryGetValue(vm, out TextBox? existing)
                && ReferenceEquals(existing, tb))
            {
                _editorByTab.Remove(vm);
            }

            if (ReferenceEquals(EditorTextBox, tb))
            {
                EditorTextBox = null;
            }
        }

        private void EditorInstance_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox tb)
            {
                return;
            }

            if (tb.DataContext is not DocumentViewModel vm)
            {
                return;
            }

            if (!ReferenceEquals(vm, ViewModel.ActiveTab))
            {
                return;
            }

            if (_previewByTab.TryGetValue(vm, out WebView2? wv))
            {
                RenderPreviewForTab(wv, tb.Text);
            }
        }

        // ----------------------------------------------------------------------
        // Per-tab preview lifecycle
        // ----------------------------------------------------------------------

        private async void PreviewInstance_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not WebView2 wv)
            {
                return;
            }

            if (wv.DataContext is not DocumentViewModel vm)
            {
                return;
            }

            _previewByTab[vm] = wv;

            try
            {
                await wv.EnsureCoreWebView2Async();

                if (ReferenceEquals(vm, ViewModel.ActiveTab))
                {
                    PreviewWebView = wv;
                }

                RenderPreviewForTab(wv, vm.MarkdownText);
            }
            catch (Exception ex)
            {
                App.Logger?.Warn(
                    $"Preview initialization failed: {ex.Message}",
                    "MainWindow");
            }
        }

        private void PreviewInstance_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is not WebView2 wv)
            {
                return;
            }

            if (wv.DataContext is not DocumentViewModel vm)
            {
                return;
            }

            if (_previewByTab.TryGetValue(vm, out WebView2? existing)
                && ReferenceEquals(existing, wv))
            {
                _previewByTab.Remove(vm);
            }

            if (ReferenceEquals(PreviewWebView, wv))
            {
                PreviewWebView = null;
            }
        }

        private void RenderPreviewForTab(WebView2 wv, string? markdown)
        {
            if (wv?.CoreWebView2 == null)
            {
                return;
            }

            try
            {
                string body = Markdig.Markdown.ToHtml(
                    markdown ?? string.Empty,
                    _markdownPipeline);

                string html = BuildHtmlDocument(body);
                wv.CoreWebView2.NavigateToString(html);
            }
            catch (Exception ex)
            {
                App.Logger?.Error("Per-tab preview render failed.", ex, "MainWindow");
            }
        }

        // ----------------------------------------------------------------------
        // TabView events
        // ----------------------------------------------------------------------

        private void DocumentTabs_AddTabButtonClick(TabView sender, object args)
        {
            ViewModel.AddNewTab(activate: true);
        }

        private void DocumentTabs_TabCloseRequested(
            TabView sender,
            TabViewTabCloseRequestedEventArgs args)
        {
            if (args.Item is DocumentViewModel vm)
            {
                ViewModel.CloseTab(vm);
            }
        }

        // ----------------------------------------------------------------------
        // List line parsing
        // ----------------------------------------------------------------------

        private sealed class ListLineMatch
        {
            public bool IsOrdered { get; set; }
            public string Indent { get; set; } = string.Empty;
            public string Marker { get; set; } = string.Empty;
            public int Number { get; set; }
            public string Content { get; set; } = string.Empty;
        }

        private static readonly System.Text.RegularExpressions.Regex _orderedListRegex =
            new System.Text.RegularExpressions.Regex(
                @"^(?<indent>\s*)(?<number>\d+)\.\s(?<content>.*)$",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private static readonly System.Text.RegularExpressions.Regex _unorderedListRegex =
            new System.Text.RegularExpressions.Regex(
                @"^(?<indent>\s*)(?<marker>[-*+])\s(?<content>.*)$",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private static ListLineMatch? MatchListLine(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return null;
            }

            System.Text.RegularExpressions.Match ordered = _orderedListRegex.Match(line);
            if (ordered.Success)
            {
                int.TryParse(ordered.Groups["number"].Value, out int number);

                return new ListLineMatch
                {
                    IsOrdered = true,
                    Indent = ordered.Groups["indent"].Value,
                    Number = number,
                    Marker = ordered.Groups["number"].Value + ".",
                    Content = ordered.Groups["content"].Value
                };
            }

            System.Text.RegularExpressions.Match unordered = _unorderedListRegex.Match(line);
            if (unordered.Success)
            {
                return new ListLineMatch
                {
                    IsOrdered = false,
                    Indent = unordered.Groups["indent"].Value,
                    Marker = unordered.Groups["marker"].Value,
                    Content = unordered.Groups["content"].Value
                };
            }

            return null;
        }

        // ------------------------------------------------------------------
        // ViewModel → Editor
        // ------------------------------------------------------------------

        private void ViewModel_PropertyChanged(
            object? sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(MainViewModel.ActiveTab):
                case nameof(MainViewModel.Document):
                    OnActiveTabChanged();
                    break;

                case nameof(MainViewModel.MarkdownText):
                    // Text updates now flow directly through the per-tab binding.
                    // We only need to refresh the preview for the active tab here.
                    if (EditorTextBox != null && PreviewWebView != null)
                    {
                        RenderPreviewForTab(PreviewWebView, EditorTextBox.Text);
                    }
                    break;

                case nameof(MainViewModel.WindowTitle):
                    Title = ViewModel.WindowTitle;
                    break;

            }
        }

        private void OnActiveTabChanged()
        {
            DocumentViewModel? active = ViewModel.ActiveTab;

            if (active == null)
            {
                EditorTextBox = null;
                PreviewWebView = null;
                return;
            }

            // Update EditorTextBox / PreviewWebView pointers to the active tab's
            // instances, if they've been created yet by the TabView.
            if (_editorByTab.TryGetValue(active, out TextBox? tb))
            {
                EditorTextBox = tb;
                tb.Focus(FocusState.Programmatic);
            }
            else
            {
                EditorTextBox = null;
            }

            if (_previewByTab.TryGetValue(active, out WebView2? wv))
            {
                PreviewWebView = wv;

                if (wv.CoreWebView2 != null)
                {
                    RenderPreviewForTab(wv, active.MarkdownText);
                }
            }
            else
            {
                PreviewWebView = null;
            }

            // Reset find/replace search state - it's now scoped to the new tab.
            _lastSearchTerm = string.Empty;
            _lastMatchCount = 0;
            _lastMatchIndex = -1;
            UpdateFindStatus(string.Empty);
        }

        // ------------------------------------------------------------------
        // Toolbar formatting
        // ------------------------------------------------------------------

        private void ViewModel_FormattingRequested(object? sender, string action)
        {
            switch (action)
            {
                case "bold":
                    WrapSelection("**", "**", "bold text");
                    break;
                case "italic":
                    WrapSelection("*", "*", "italic text");
                    break;
                case "code":
                    WrapSelection("`", "`", "code");
                    break;
                case "heading":
                    InsertLinePrefix("## ", "Heading");
                    break;
                case "bullet":
                    InsertLinePrefix("- ", "List item");
                    break;
                case "link":
                    InsertLink();
                    break;
            }
        }

        // ------------------------------------------------------------------
        // Export to PDF
        // ------------------------------------------------------------------
        private async void ViewModel_ExportPdfRequested(object? sender, EventArgs e)
        {
            await ExportPdfAsync();
        }

        private async Task ExportPdfAsync()
        {
            try
            {
                if (App.PdfExport == null)
                {
                    ViewModel.StatusMessage = "PDF service unavailable";
                    return;
                }

                if (PreviewWebView == null || EditorTextBox == null)
                {
                    ViewModel.StatusMessage = "Nothing to export - open a document first";
                    return;
                }

                // Step 1: Suggest a filename based on the current document.
                string suggestedName = "document";
                string? currentFilePath = ViewModel.Document?.FilePath;
                string? currentTitle = ViewModel.Document?.Title;

                if (!string.IsNullOrWhiteSpace(currentFilePath))
                {
                    suggestedName = System.IO.Path.GetFileNameWithoutExtension(currentFilePath);
                }
                else if (!string.IsNullOrWhiteSpace(currentTitle))
                {
                    suggestedName = System.IO.Path.GetFileNameWithoutExtension(currentTitle);
                }

                if (string.IsNullOrWhiteSpace(suggestedName))
                {
                    suggestedName = "document";
                }

                // Step 2: Show a Save File dialog for the destination PDF.
                IntPtr hwnd = WindowNative.GetWindowHandle(this);

                string? outputPath = MarkdownEditor.Helpers.Win32FileDialogs.ShowSaveDialog(
                    ownerHandle: hwnd,
                    title: "Export to PDF",
                    filter: "PDF Files (*.pdf)|*.pdf|All Files (*.*)|*.*",
                    initialDirectory: GetExportInitialDirectory(),
                    suggestedFileName: suggestedName + ".pdf");

                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    ViewModel.StatusMessage = "Export canceled";
                    return;
                }

                // Force the .pdf extension just in case the user typed a different one.
                if (!outputPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    outputPath += ".pdf";
                }

                ViewModel.StatusMessage = "Exporting to PDF...";

                // Step 3: Build the styled HTML from current Markdown.
                string markdown = EditorTextBox.Text ?? string.Empty;
                string body = Markdig.Markdown.ToHtml(markdown, _markdownPipeline);
                string html = BuildHtmlDocument(body);

                // Step 4: Render via the already-initialized preview WebView2.
                bool ok = await App.PdfExport.ExportHtmlToPdfAsync(
                    PreviewWebView,
                    html,
                    outputPath);

                if (!ok)
                {
                    ViewModel.StatusMessage = "PDF export failed";
                    return;
                }

                // Step 5: Restore the preview to current content (the WebView was used
                // to render the export and its content has been replaced).
                RenderPreview(EditorTextBox.Text ?? string.Empty);

                ViewModel.StatusMessage = $"Exported {System.IO.Path.GetFileName(outputPath)}";

                App.Logger?.Info($"PDF export complete: {outputPath}", "MainWindow");

                // Step 6: Offer to open the resulting PDF.
                TryOpenFile(outputPath);
            }
            catch (Exception ex)
            {
                App.Logger?.Error("ExportPdfAsync failed.", ex, "MainWindow");
                ViewModel.StatusMessage = "PDF export failed";
            }
        }

        private string GetExportInitialDirectory()
        {
            try
            {
                string? currentFilePath = ViewModel.Document?.FilePath;

                if (!string.IsNullOrWhiteSpace(currentFilePath))
                {
                    string? dir = System.IO.Path.GetDirectoryName(currentFilePath);
                    if (!string.IsNullOrWhiteSpace(dir) && System.IO.Directory.Exists(dir))
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

        private void TryOpenFile(string path)
        {
            try
            {
                if (!System.IO.File.Exists(path))
                {
                    return;
                }

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                };

                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                App.Logger?.Warn($"Could not open exported file: {ex.Message}", "MainWindow");
            }
        }

        // ----------------------------------------------------------------------
        // Find / Replace - Show / Hide
        // ----------------------------------------------------------------------

        private void ShowFindPanel(bool showReplace)
        {
            FindReplacePanel.Visibility = Visibility.Visible;
            _isFindPanelVisible = true;

            if (showReplace)
            {
                ToggleReplaceButton.IsChecked = true;
                ReplaceRow.Visibility = Visibility.Visible;
            }

            // Pre-seed the find box with current selection if anything is selected.
            string selected = EditorTextBox.SelectedText ?? string.Empty;

            if (!string.IsNullOrEmpty(selected) && selected.Length < 200)
            {
                _suppressFindSearch = true;
                FindTextBox.Text = selected;
                _suppressFindSearch = false;
            }

            FindTextBox.Focus(FocusState.Programmatic);
            FindTextBox.SelectAll();

            UpdateFindStatus(string.Empty);

            // If there's already text in the find box, re-run the search.
            if (!string.IsNullOrEmpty(FindTextBox.Text))
            {
                RunSearch(resetIndex: true, advance: false);
            }
        }

        // ----------------------------------------------------------------------
        // Find / Replace - Keyboard accelerators
        // ----------------------------------------------------------------------

        private void ShowFindAccelerator_Invoked(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args)
        {
            ShowFindPanel(showReplace: false);
            args.Handled = true;
        }

        private void ShowReplaceAccelerator_Invoked(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args)
        {
            ShowFindPanel(showReplace: true);
            args.Handled = true;
        }

        private void FindNextAccelerator_Invoked(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!_isFindPanelVisible)
            {
                ShowFindPanel(showReplace: false);
            }
            else if (!string.IsNullOrEmpty(FindTextBox.Text))
            {
                RunSearch(resetIndex: false, advance: true, reverse: false);
            }

            args.Handled = true;
        }

        private void FindPreviousAccelerator_Invoked(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!_isFindPanelVisible)
            {
                ShowFindPanel(showReplace: false);
            }
            else if (!string.IsNullOrEmpty(FindTextBox.Text))
            {
                RunSearch(resetIndex: false, advance: true, reverse: true);
            }

            args.Handled = true;
        }

        // ----------------------------------------------------------------------
        // Markdown Shortcuts - Bold / Italic
        // ----------------------------------------------------------------------

        private void BoldAccelerator_Invoked(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args)
        {
            if (EditorTextBox.FocusState != FocusState.Unfocused)
            {
                WrapSelection("**", "**", "bold text");
            }

            args.Handled = true;
        }

        private void ItalicAccelerator_Invoked(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args)
        {
            if (EditorTextBox.FocusState != FocusState.Unfocused)
            {
                WrapSelection("*", "*", "italic text");
            }

            args.Handled = true;
        }

        // ----------------------------------------------------------------------
        // Markdown Shortcuts - Heading cycling
        // ----------------------------------------------------------------------

        private void Heading1Accelerator_Invoked(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args)
        {
            if (EditorTextBox.FocusState != FocusState.Unfocused)
            {
                ToggleHeading(1);
            }

            args.Handled = true;
        }

        private void Heading2Accelerator_Invoked(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args)
        {
            if (EditorTextBox.FocusState != FocusState.Unfocused)
            {
                ToggleHeading(2);
            }

            args.Handled = true;
        }

        private void Heading3Accelerator_Invoked(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args)
        {
            if (EditorTextBox.FocusState != FocusState.Unfocused)
            {
                ToggleHeading(3);
            }

            args.Handled = true;
        }

        private void Heading4Accelerator_Invoked(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args)
        {
            if (EditorTextBox.FocusState != FocusState.Unfocused)
            {
                ToggleHeading(4);
            }

            args.Handled = true;
        }

        private void HeadingClearAccelerator_Invoked(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args)
        {
            if (EditorTextBox.FocusState != FocusState.Unfocused)
            {
                ToggleHeading(0);
            }

            args.Handled = true;
        }

        private void ToggleHeading(int level)
        {
            try
            {
                string text = EditorTextBox.Text ?? string.Empty;
                int caret = EditorTextBox.SelectionStart;

                // Walk backward to find the current line's start.
                int lineStart = caret;
                while (lineStart > 0
                    && text[lineStart - 1] != '\n'
                    && text[lineStart - 1] != '\r')
                {
                    lineStart--;
                }

                // Walk forward to find the current line's end.
                int lineEnd = caret;
                while (lineEnd < text.Length
                    && text[lineEnd] != '\n'
                    && text[lineEnd] != '\r')
                {
                    lineEnd++;
                }

                string currentLine = text.Substring(lineStart, lineEnd - lineStart);

                // Strip any existing heading prefix.
                int existingLevel = 0;
                int contentStart = 0;

                while (contentStart < currentLine.Length
                    && currentLine[contentStart] == '#'
                    && existingLevel < 6)
                {
                    existingLevel++;
                    contentStart++;
                }

                if (existingLevel > 0
                    && contentStart < currentLine.Length
                    && currentLine[contentStart] == ' ')
                {
                    contentStart++;
                }
                else if (existingLevel > 0)
                {
                    // It was a string of # without a trailing space - not actually a heading.
                    existingLevel = 0;
                    contentStart = 0;
                }

                string body = currentLine.Substring(contentStart);

                // Determine the new prefix.
                string newPrefix;

                if (level == 0)
                {
                    // Always clear.
                    newPrefix = string.Empty;
                }
                else if (existingLevel == level)
                {
                    // Same level - toggle off.
                    newPrefix = string.Empty;
                }
                else
                {
                    newPrefix = new string('#', level) + " ";
                }

                string newLine = newPrefix + body;

                // Replace the current line.
                EditorTextBox.SelectionStart = lineStart;
                EditorTextBox.SelectionLength = lineEnd - lineStart;
                EditorTextBox.SelectedText = newLine;

                // Position the caret at the end of the new line.
                int newCaret = lineStart + newLine.Length;
                EditorTextBox.SelectionStart = newCaret;
                EditorTextBox.SelectionLength = 0;
            }
            catch (Exception ex)
            {
                App.Logger?.Warn($"ToggleHeading failed: {ex.Message}", "Markdown");
            }
        }

        // ----------------------------------------------------------------------
        // Find / Replace - Toolbar buttons
        // ----------------------------------------------------------------------

        private void FindToolbarButton_Click(object sender, RoutedEventArgs e)
        {
            ShowFindPanel(showReplace: false);
        }

        private void ReplaceToolbarButton_Click(object sender, RoutedEventArgs e)
        {
            ShowFindPanel(showReplace: true);
        }

        private void HideFindPanel()
        {
            FindReplacePanel.Visibility = Visibility.Collapsed;
            _isFindPanelVisible = false;

            _lastSearchTerm = string.Empty;
            _lastMatchCount = 0;
            _lastMatchIndex = -1;

            EditorTextBox.Focus(FocusState.Programmatic);
        }

        private void CloseFindButton_Click(object sender, RoutedEventArgs e)
        {
            HideFindPanel();
        }

        private void ToggleReplaceButton_Toggled(object sender, RoutedEventArgs e)
        {
            bool show = ToggleReplaceButton.IsChecked == true;
            ReplaceRow.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

            if (show)
            {
                ReplaceTextBox.Focus(FocusState.Programmatic);
            }
            else
            {
                FindTextBox.Focus(FocusState.Programmatic);
            }
        }

        // ----------------------------------------------------------------------
        // Find / Replace - Find input events
        // ----------------------------------------------------------------------

        private void FindTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressFindSearch)
            {
                return;
            }

            RunSearch(resetIndex: true, advance: false);
        }

        private void FindTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                bool shift = (Microsoft.UI.Input.InputKeyboardSource
                    .GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift)
                    & Windows.UI.Core.CoreVirtualKeyStates.Down)
                    == Windows.UI.Core.CoreVirtualKeyStates.Down;

                if (shift)
                {
                    RunSearch(resetIndex: false, advance: true, reverse: true);
                }
                else
                {
                    RunSearch(resetIndex: false, advance: true, reverse: false);
                }

                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Escape)
            {
                HideFindPanel();
                e.Handled = true;
            }
        }

        private void ReplaceTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                ReplaceOne();
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Escape)
            {
                HideFindPanel();
                e.Handled = true;
            }
        }

        private void FindNextButton_Click(object sender, RoutedEventArgs e)
        {
            RunSearch(resetIndex: false, advance: true, reverse: false);
        }

        private void FindPreviousButton_Click(object sender, RoutedEventArgs e)
        {
            RunSearch(resetIndex: false, advance: true, reverse: true);
        }

        private void MatchOptions_Changed(object sender, RoutedEventArgs e)
        {
            // Re-run search on changing case or whole-word options.
            if (_isFindPanelVisible && !string.IsNullOrEmpty(FindTextBox.Text))
            {
                RunSearch(resetIndex: true, advance: false);
            }
        }

        // ----------------------------------------------------------------------
        // Find / Replace - Replace events
        // ----------------------------------------------------------------------

        private void ReplaceOneButton_Click(object sender, RoutedEventArgs e)
        {
            ReplaceOne();
        }

        private void ReplaceAllButton_Click(object sender, RoutedEventArgs e)
        {
            ReplaceAll();
        }

        // ----------------------------------------------------------------------
        // Search core
        // ----------------------------------------------------------------------

        private void RunSearch(bool resetIndex, bool advance, bool reverse = false)
        {
            try
            {
                string needle = FindTextBox.Text ?? string.Empty;
                string haystack = EditorTextBox.Text ?? string.Empty;

                bool matchCase = MatchCaseButton.IsChecked == true;
                bool wholeWord = MatchWholeWordButton.IsChecked == true;

                _lastMatchCase = matchCase;
                _lastMatchWholeWord = wholeWord;
                _lastSearchTerm = needle;

                if (string.IsNullOrEmpty(needle) || string.IsNullOrEmpty(haystack))
                {
                    _lastMatchCount = 0;
                    _lastMatchIndex = -1;
                    UpdateFindStatus(string.Empty);
                    return;
                }

                List<int> matches = FindAllMatches(haystack, needle, matchCase, wholeWord);

                _lastMatchCount = matches.Count;

                if (matches.Count == 0)
                {
                    _lastMatchIndex = -1;
                    UpdateFindStatus("No matches");
                    return;
                }

                int currentCaret = EditorTextBox.SelectionStart;
                int targetIndex;

                if (resetIndex)
                {
                    // Find first match at or after caret; fall back to first match.
                    targetIndex = matches.FindIndex(m => m >= currentCaret);
                    if (targetIndex < 0)
                    {
                        targetIndex = 0;
                    }
                }
                else if (advance)
                {
                    if (reverse)
                    {
                        // Find last match strictly before caret; fall back to last match (wrap).
                        targetIndex = matches.FindLastIndex(m => m < currentCaret);
                        if (targetIndex < 0)
                        {
                            targetIndex = matches.Count - 1;
                        }
                    }
                    else
                    {
                        // Find first match strictly after caret; fall back to first match (wrap).
                        int caretEnd = currentCaret + EditorTextBox.SelectionLength;
                        targetIndex = matches.FindIndex(m => m >= caretEnd);
                        if (targetIndex < 0)
                        {
                            targetIndex = 0;
                        }
                    }
                }
                else
                {
                    targetIndex = Math.Max(0, _lastMatchIndex);
                    if (targetIndex >= matches.Count)
                    {
                        targetIndex = matches.Count - 1;
                    }
                }

                _lastMatchIndex = targetIndex;

                int matchStart = matches[targetIndex];
                int matchLength = needle.Length;

                EditorTextBox.SelectionStart = matchStart;
                EditorTextBox.SelectionLength = matchLength;

                EnsureSelectionVisible();

                UpdateFindStatus($"Match {targetIndex + 1} of {matches.Count}");
            }
            catch (Exception ex)
            {
                App.Logger?.Error("RunSearch failed.", ex, "FindReplace");
                UpdateFindStatus("Search error");
            }
        }

        private void ReplaceOne()
        {
            try
            {
                string needle = FindTextBox.Text ?? string.Empty;
                string replacement = ReplaceTextBox.Text ?? string.Empty;

                if (string.IsNullOrEmpty(needle))
                {
                    return;
                }

                bool matchCase = MatchCaseButton.IsChecked == true;

                // If the current selection matches the search term, replace it.
                string currentSelection = EditorTextBox.SelectedText ?? string.Empty;

                bool selectionMatches = matchCase
                    ? string.Equals(currentSelection, needle, StringComparison.Ordinal)
                    : string.Equals(currentSelection, needle, StringComparison.OrdinalIgnoreCase);

                if (selectionMatches)
                {
                    int start = EditorTextBox.SelectionStart;
                    EditorTextBox.SelectedText = replacement;

                    EditorTextBox.SelectionStart = start + replacement.Length;
                    EditorTextBox.SelectionLength = 0;

                    UpdateFindStatus($"Replaced 1");
                }

                // Then advance to the next match.
                RunSearch(resetIndex: false, advance: true, reverse: false);
            }
            catch (Exception ex)
            {
                App.Logger?.Error("ReplaceOne failed.", ex, "FindReplace");
                UpdateFindStatus("Replace error");
            }
        }

        private void ReplaceAll()
        {
            try
            {
                string needle = FindTextBox.Text ?? string.Empty;
                string replacement = ReplaceTextBox.Text ?? string.Empty;
                string haystack = EditorTextBox.Text ?? string.Empty;

                if (string.IsNullOrEmpty(needle))
                {
                    return;
                }

                bool matchCase = MatchCaseButton.IsChecked == true;
                bool wholeWord = MatchWholeWordButton.IsChecked == true;

                List<int> matches = FindAllMatches(haystack, needle, matchCase, wholeWord);

                if (matches.Count == 0)
                {
                    UpdateFindStatus("No matches");
                    return;
                }

                // Replace from end to start so earlier offsets remain valid.
                System.Text.StringBuilder sb = new System.Text.StringBuilder(haystack);

                for (int i = matches.Count - 1; i >= 0; i--)
                {
                    sb.Remove(matches[i], needle.Length);
                    sb.Insert(matches[i], replacement);
                }

                string newText = sb.ToString();

                // Use SelectedText pattern across the whole document so undo is preserved.
                EditorTextBox.SelectAll();
                EditorTextBox.SelectedText = newText;

                EditorTextBox.SelectionStart = 0;
                EditorTextBox.SelectionLength = 0;

                UpdateFindStatus($"Replaced {matches.Count} occurrences");

                App.Logger?.Info(
                    $"Replaced {matches.Count} occurrences of '{needle}'.",
                    "FindReplace");
            }
            catch (Exception ex)
            {
                App.Logger?.Error("ReplaceAll failed.", ex, "FindReplace");
                UpdateFindStatus("Replace error");
            }
        }

        // ----------------------------------------------------------------------
        // Search utilities
        // ----------------------------------------------------------------------

        private static List<int> FindAllMatches(
            string haystack,
            string needle,
            bool matchCase,
            bool wholeWord)
        {
            List<int> matches = new List<int>();

            if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle))
            {
                return matches;
            }

            StringComparison cmp = matchCase
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            int searchFrom = 0;

            while (searchFrom <= haystack.Length - needle.Length)
            {
                int index = haystack.IndexOf(needle, searchFrom, cmp);

                if (index < 0)
                {
                    break;
                }

                if (wholeWord)
                {
                    bool leftOk = index == 0 || !IsWordChar(haystack[index - 1]);
                    int rightIndex = index + needle.Length;
                    bool rightOk = rightIndex >= haystack.Length || !IsWordChar(haystack[rightIndex]);

                    if (leftOk && rightOk)
                    {
                        matches.Add(index);
                    }
                }
                else
                {
                    matches.Add(index);
                }

                searchFrom = index + Math.Max(1, needle.Length);
            }

            return matches;
        }

        private static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        private void UpdateFindStatus(string message)
        {
            if (FindStatusText != null)
            {
                FindStatusText.Text = message ?? string.Empty;
            }
        }

        private void EnsureSelectionVisible()
        {
            try
            {
                // Briefly focus the editor to ensure the selection is scrolled into view,
                // then return focus to the Find box so the user can keep typing.
                bool findHasFocus =
                    FindTextBox != null
                    && FindTextBox.FocusState != FocusState.Unfocused;

                bool replaceHasFocus =
                    ReplaceTextBox != null
                    && ReplaceTextBox.FocusState != FocusState.Unfocused;

                EditorTextBox.Focus(FocusState.Programmatic);

                if (replaceHasFocus)
                {
                    ReplaceTextBox?.Focus(FocusState.Programmatic);
                }
                else if (findHasFocus)
                {
                    FindTextBox?.Focus(FocusState.Programmatic);
                }
            }
            catch (Exception ex)
            {
                App.Logger?.Warn($"EnsureSelectionVisible failed: {ex.Message}", "FindReplace");
            }
        }


        private void WrapSelection(string prefix, string suffix, string placeholder)
        {
            string current = EditorTextBox.SelectedText ?? string.Empty;

            string newText = string.IsNullOrEmpty(current)
                ? prefix + placeholder + suffix
                : prefix + current + suffix;

            EditorTextBox.SelectedText = newText;
            EditorTextBox.Focus(FocusState.Programmatic);
        }

        private void InsertLinePrefix(string linePrefix, string placeholder)
        {
            string current = EditorTextBox.SelectedText ?? string.Empty;

            string newText = string.IsNullOrEmpty(current)
                ? linePrefix + placeholder
                : linePrefix + current;

            EditorTextBox.SelectedText = newText;
            EditorTextBox.Focus(FocusState.Programmatic);
        }

        private void InsertLink()
        {
            string current = EditorTextBox.SelectedText ?? string.Empty;

            string label = string.IsNullOrEmpty(current) ? "link text" : current;
            string markdown = "[" + label + "](https://example.com)";

            EditorTextBox.SelectedText = markdown;
            EditorTextBox.Focus(FocusState.Programmatic);
        }



        // ------------------------------------------------------------------
        // Preview rendering
        // ------------------------------------------------------------------

        private void RenderPreview(string markdown)
        {
            if (PreviewWebView?.CoreWebView2 == null)
            {
                return;
            }

            try
            {
                string body = Markdig.Markdown.ToHtml(
                    markdown ?? string.Empty,
                    _markdownPipeline);

                string html = BuildHtmlDocument(body);

                PreviewWebView.NavigateToString(html);
            }
            catch (Exception ex)
            {
                App.Logger?.Error("Preview render failed.", ex, "MainWindow");
            }
        }

        private void RecentFilesFlyout_Opening(object sender, object e)
        {
            try
            {
                if (sender is not MenuFlyout flyout)
                {
                    return;
                }

                flyout.Items.Clear();

                var recent = App.Settings?.Current.RecentFiles;

                if (recent == null || recent.Count == 0)
                {
                    var empty = new MenuFlyoutItem
                    {
                        Text = "No recent files",
                        IsEnabled = false
                    };

                    flyout.Items.Add(empty);
                    return;
                }

                foreach (string path in recent)
                {
                    string fileName = System.IO.Path.GetFileName(path);
                    string display = string.IsNullOrWhiteSpace(fileName) ? path : fileName;

                    var item = new MenuFlyoutItem
                    {
                        Text = display,
                        Tag = path
                    };

                    ToolTipService.SetToolTip(item, path);

                    item.Click += RecentFileItem_Click;
                    flyout.Items.Add(item);
                }

                flyout.Items.Add(new MenuFlyoutSeparator());

                var clearItem = new MenuFlyoutItem
                {
                    Text = "Clear Recent Files",
                    Icon = new SymbolIcon(Symbol.Delete)
                };

                clearItem.Click += ClearRecentFiles_Click;
                flyout.Items.Add(clearItem);
            }
            catch (Exception ex)
            {
                App.Logger?.Error("Failed to populate recent files menu.", ex, "MainWindow");
            }
        }

        private async void RecentFileItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuFlyoutItem item && item.Tag is string path)
                {
                    await ViewModel.OpenRecentAsync(path);
                }
            }
            catch (Exception ex)
            {
                App.Logger?.Error("RecentFileItem_Click failed.", ex, "MainWindow");
            }
        }

        private void ClearRecentFiles_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (App.Settings != null)
                {
                    App.Settings.Current.RecentFiles.Clear();
                    App.Settings.Save();

                    App.Logger?.Info("Recent files cleared by user.", "MainWindow");
                }
            }
            catch (Exception ex)
            {
                App.Logger?.Error("ClearRecentFiles_Click failed.", ex, "MainWindow");
            }
        }

        private async void TemplatesFlyout_Opening(object sender, object e)
        {
            try
            {
                if (sender is not MenuFlyout flyout)
                {
                    return;
                }

                flyout.Items.Clear();

                // Show a transient "Loading..." item so the menu doesn't appear empty.
                var loading = new MenuFlyoutItem
                {
                    Text = "Loading templates...",
                    IsEnabled = false
                };
                flyout.Items.Add(loading);

                ITemplateService? templateService = App.Templates;

                if (templateService == null)
                {
                    flyout.Items.Clear();

                    flyout.Items.Add(new MenuFlyoutItem
                    {
                        Text = "Template service unavailable",
                        IsEnabled = false
                    });

                    return;
                }

                IReadOnlyList<TemplateInfo> templates =
                    await templateService.GetTemplatesAsync();

                flyout.Items.Clear();

                if (templates == null || templates.Count == 0)
                {
                    var empty = new MenuFlyoutItem
                    {
                        Text = "No templates found",
                        IsEnabled = false
                    };

                    flyout.Items.Add(empty);
                }
                else
                {
                    foreach (TemplateInfo template in templates)
                    {
                        var item = new MenuFlyoutItem
                        {
                            Text = template.Name,
                            Tag = template
                        };

                        ToolTipService.SetToolTip(item, template.FilePath);

                        item.Click += TemplateItem_Click;
                        flyout.Items.Add(item);
                    }
                }

                flyout.Items.Add(new MenuFlyoutSeparator());

                var refreshItem = new MenuFlyoutItem
                {
                    Text = "Refresh templates",
                    Icon = new SymbolIcon(Symbol.Refresh)
                };

                refreshItem.Click += RefreshTemplates_Click;
                flyout.Items.Add(refreshItem);

                var openFolderItem = new MenuFlyoutItem
                {
                    Text = "Open templates folder",
                    Icon = new SymbolIcon(Symbol.Folder)
                };

                openFolderItem.Click += OpenTemplatesFolder_Click;
                flyout.Items.Add(openFolderItem);
            }
            catch (Exception ex)
            {
                App.Logger?.Error("Failed to populate templates menu.", ex, "MainWindow");
            }
        }

        private async void TemplateItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuFlyoutItem item && item.Tag is TemplateInfo template)
                {
                    await ViewModel.NewFromTemplateAsync(template.FilePath, template.Name);
                }
            }
            catch (Exception ex)
            {
                App.Logger?.Error("TemplateItem_Click failed.", ex, "MainWindow");
            }
        }

        private void RefreshTemplates_Click(object sender, RoutedEventArgs e)
        {
            // Closing the flyout and reopening it triggers a fresh scan via the
            // Opening event. The user simply opens the menu again.
            ViewModel.StatusMessage = "Templates will reload on next open.";

            App.Logger?.Info("User requested templates refresh.", "MainWindow");
        }

        private void OpenTemplatesFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (App.Templates == null)
                {
                    return;
                }

                string folder = App.Templates.TemplatesFolderPath;

                if (string.IsNullOrWhiteSpace(folder) || !System.IO.Directory.Exists(folder))
                {
                    ViewModel.StatusMessage = "Templates folder not found";
                    return;
                }

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                };

                System.Diagnostics.Process.Start(psi);

                App.Logger?.Info($"Opened templates folder: {folder}", "MainWindow");
            }
            catch (Exception ex)
            {
                App.Logger?.Error("Failed to open templates folder.", ex, "MainWindow");
                ViewModel.StatusMessage = "Failed to open templates folder";
            }
        }

        private string BuildHtmlDocument(string bodyHtml)
        {
            bool dark = IsEffectiveDarkTheme();

            string bg = dark ? "#1e1e1e" : "#ffffff";
            string fg = dark ? "#e8e8e8" : "#222222";
            string headingColor = dark ? "#6FB1FF" : "#0F4E92";
            string codeBg = dark ? "#2d2d2d" : "#f3f3f3";
            string preBg = dark ? "#2d2d2d" : "#f6f8fa";
            string quoteBg = dark ? "#252525" : "#f9f9f9";
            string quoteFg = dark ? "#bbbbbb" : "#555555";
            string tableHeaderBg = dark ? "#2d2d2d" : "#f3f3f3";
            string borderColor = dark ? "#444444" : "#dddddd";

            string css =
                "body { font-family: 'Segoe UI', Arial, sans-serif; font-size: 15px; " +
                "       padding: 16px; line-height: 1.5; " +
                "       color: " + fg + "; background: " + bg + "; } " +
                "h1, h2, h3, h4 { color: " + headingColor + "; } " +
                "code { font-family: Consolas, 'Courier New', monospace; " +
                "       background: " + codeBg + "; padding: 2px 4px; border-radius: 3px; } " +
                "pre { background: " + preBg + "; padding: 12px; border-radius: 6px; " +
                "      overflow-x: auto; } " +
                "pre code { background: none; padding: 0; } " +
                "blockquote { border-left: 4px solid " + headingColor + "; margin: 0; " +
                "             padding: 4px 12px; color: " + quoteFg + "; background: " + quoteBg + "; } " +
                "table { border-collapse: collapse; margin: 8px 0; } " +
                "th, td { border: 1px solid " + borderColor + "; padding: 6px 10px; } " +
                "th { background: " + tableHeaderBg + "; } " +
                "a { color: " + headingColor + "; }";

            return
                "<!DOCTYPE html>" +
                "<html><head><meta charset='utf-8'>" +
                "<style>" + css + "</style>" +
                "</head><body>" +
                bodyHtml +
                "</body></html>";
        }

        // ------------------------------------------------------------------
        // Environment badge
        // ------------------------------------------------------------------

        private void UpdateEnvironmentBadge()
        {
            try
            {
                RuntimeEnvironmentInfo env = PortableModeDetector.GetEnvironment();

                string mode = env.IsPortable ? "Portable" : "Fallback";
                string drive = string.IsNullOrEmpty(env.DriveLetter)
                    ? "Unknown"
                    : env.DriveLetter + " [" + env.DriveKind + "]";

                EnvBadge.Text = mode + " | " + drive + " | User: " + env.UserName;
            }
            catch
            {
                EnvBadge.Text = string.Empty;
            }
        }
    }
}