using System;
using System.IO;
using System.Threading.Tasks;

using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace MarkdownEditor.Services;

/// <summary>
/// Default PDF export service.
/// Uses CoreWebView2.PrintToPdfAsync to render the editor preview as a PDF.
/// </summary>
public sealed class PdfExportService : IPdfExportService
{
    private readonly ILoggingService? _logger;

    public PdfExportService(ILoggingService? logger = null)
    {
        _logger = logger;
    }

    public async Task<bool> ExportHtmlToPdfAsync(
        WebView2 webView,
        string html,
        string outputPath)
    {
        if (webView == null)
        {
            _logger?.Warn("PDF export called with null WebView2.", "PdfExport");
            return false;
        }

        if (webView.CoreWebView2 == null)
        {
            _logger?.Warn("PDF export called before WebView2 was initialized.", "PdfExport");
            return false;
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            _logger?.Warn("PDF export called with empty output path.", "PdfExport");
            return false;
        }

        try
        {
            string directory = Path.GetDirectoryName(outputPath) ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Render the supplied HTML into the WebView, then wait for the
            // navigation to complete before printing.
            TaskCompletionSource<bool> navTcs =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnNavigationCompleted(
                CoreWebView2 sender,
                CoreWebView2NavigationCompletedEventArgs args)
            {
                navTcs.TrySetResult(args.IsSuccess);
            }

            webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;

            try
            {
                webView.CoreWebView2.NavigateToString(html ?? string.Empty);

                bool navigated = await navTcs.Task.ConfigureAwait(true);

                if (!navigated)
                {
                    _logger?.Warn(
                        "WebView2 navigation failed; PDF will not be created.",
                        "PdfExport");
                    return false;
                }
            }
            finally
            {
                webView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
            }

            // Configure the print settings (margins are in inches).
            CoreWebView2PrintSettings settings =
                webView.CoreWebView2.Environment.CreatePrintSettings();

            settings.MarginTop = 0.75;
            settings.MarginBottom = 0.75;
            settings.MarginLeft = 0.75;
            settings.MarginRight = 0.75;
            settings.Orientation = CoreWebView2PrintOrientation.Portrait;
            settings.ScaleFactor = 1.0;
            settings.ShouldPrintBackgrounds = true;
            settings.ShouldPrintHeaderAndFooter = false;

            // Atomic write: render to a temp file then move into place.
            string tempPath = outputPath + ".tmp";

            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            bool printed = await webView.CoreWebView2
                .PrintToPdfAsync(tempPath, settings)
                .AsTask()
                .ConfigureAwait(true);

            if (!printed || !File.Exists(tempPath))
            {
                _logger?.Warn("PrintToPdfAsync returned false.", "PdfExport");
                return false;
            }

            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            File.Move(tempPath, outputPath);

            _logger?.Info($"PDF exported: {outputPath}", "PdfExport");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.Error($"PDF export failed for {outputPath}.", ex, "PdfExport");
            return false;
        }
    }
}