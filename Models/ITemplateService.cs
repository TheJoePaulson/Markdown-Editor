using System.Collections.Generic;
using System.Threading.Tasks;

using MarkdownEditor.Models;

namespace MarkdownEditor.Services;

/// <summary>
/// Service for discovering and loading Markdown templates from disk.
/// </summary>
public interface ITemplateService
{
    /// <summary>
    /// Returns the absolute path of the Templates folder.
    /// </summary>
    string TemplatesFolderPath { get; }

    /// <summary>
    /// Discovers all .md files in the Templates folder.
    /// Returns an empty list if the folder is missing or empty.
    /// </summary>
    Task<IReadOnlyList<TemplateInfo>> GetTemplatesAsync();

    /// <summary>
    /// Reads the full content of a template file from disk.
    /// Returns an empty string if the file cannot be read.
    /// </summary>
    Task<string> LoadTemplateAsync(string filePath);
}