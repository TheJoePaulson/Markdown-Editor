using System;
using System.Threading.Tasks;

using MarkdownEditor.Models;

namespace MarkdownEditor.Services;

/// <summary>
/// Result of a successful open operation.
/// </summary>
public sealed class OpenFileResult
{
    public string FilePath { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Contract for the Markdown file service.
/// Knows how to read, write, prompt for, and back up Markdown files.
/// </summary>
public interface IMarkdownFileService
{
    /// <summary>
    /// Prompts the user to pick a Markdown file and returns its content.
    /// Returns null if canceled.
    /// </summary>
    Task<OpenFileResult?> OpenAsync(IntPtr ownerHandle);

    /// <summary>
    /// Saves the document to its existing FilePath.
    /// If no path exists, falls back to SaveAsAsync.
    /// </summary>
    Task<bool> SaveAsync(MarkdownDocument document, IntPtr ownerHandle);

    /// <summary>
    /// Prompts the user for a path and saves the document there.
    /// Returns the saved path, or null if canceled.
    /// </summary>
    Task<string?> SaveAsAsync(MarkdownDocument document, IntPtr ownerHandle);
}