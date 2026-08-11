using System.Threading.Tasks;

using Microsoft.UI.Xaml.Controls;

namespace MarkdownEditor.Services;

/// <summary>
/// Service for exporting rendered Markdown to PDF using WebView2.
/// </summary>
public interface IPdfExportService
{
    /// <summary>
    /// Renders the given HTML into the supplied (already-initialized) WebView2,
    /// then exports the page to a PDF file at the given path.
    /// </summary>
    /// <param name="webView">
    /// A WebView2 instance whose CoreWebView2 is already initialized.
    /// </param>
    /// <param name="html">The HTML content to render.</param>
    /// <param name="outputPath">Final PDF file path.</param>
    /// <returns>True on success, false on failure.</returns>
    Task<bool> ExportHtmlToPdfAsync(
        WebView2 webView,
        string html,
        string outputPath);
}