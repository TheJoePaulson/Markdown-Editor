using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MarkdownEditor.Helpers;
using MarkdownEditor.Models;

namespace MarkdownEditor.Services;

/// <summary>
/// Default template service.
/// Discovers .md files in the AppFolders.Templates folder.
/// </summary>
public sealed class TemplateService : ITemplateService
{
    // ----------------------------------------------------------------------
    // Configuration
    // ----------------------------------------------------------------------

    private static readonly string[] AllowedExtensions =
        new[] { ".md", ".markdown" };

    // ----------------------------------------------------------------------
    // Dependencies
    // ----------------------------------------------------------------------

    private readonly ILoggingService? _logger;
    private readonly string _templatesFolder;

    // ----------------------------------------------------------------------
    // Properties
    // ----------------------------------------------------------------------

    public string TemplatesFolderPath => _templatesFolder;

    // ----------------------------------------------------------------------
    // Construction
    // ----------------------------------------------------------------------

    public TemplateService(ILoggingService? logger = null)
    {
        _logger = logger;

        AppFolders.Initialize();
        _templatesFolder = AppFolders.Templates;

        _logger?.Info($"Template folder: {_templatesFolder}", "Templates");
    }

    // ----------------------------------------------------------------------
    // Public API
    // ----------------------------------------------------------------------

    public Task<IReadOnlyList<TemplateInfo>> GetTemplatesAsync()
    {
        return Task.Run<IReadOnlyList<TemplateInfo>>(() =>
        {
            List<TemplateInfo> results = new List<TemplateInfo>();

            try
            {
                if (!Directory.Exists(_templatesFolder))
                {
                    _logger?.Warn(
                        $"Templates folder not found: {_templatesFolder}",
                        "Templates");
                    return results;
                }

                string[] files = Directory.GetFiles(
                    _templatesFolder,
                    "*.*",
                    SearchOption.TopDirectoryOnly);

                foreach (string file in files)
                {
                    try
                    {
                        string extension = Path.GetExtension(file);

                        bool isAllowed = AllowedExtensions
                            .Any(ext => string.Equals(
                                ext,
                                extension,
                                StringComparison.OrdinalIgnoreCase));

                        if (!isAllowed)
                        {
                            continue;
                        }

                        FileInfo info = new FileInfo(file);

                        results.Add(new TemplateInfo
                        {
                            Name = Path.GetFileNameWithoutExtension(file),
                            FilePath = info.FullName,
                            LastModifiedUtc = info.LastWriteTimeUtc
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger?.Warn(
                            $"Skipping template file {file}: {ex.Message}",
                            "Templates");
                    }
                }

                results = results
                    .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                _logger?.Info(
                    $"Discovered {results.Count} template(s).",
                    "Templates");
            }
            catch (Exception ex)
            {
                _logger?.Error("Failed to enumerate templates.", ex, "Templates");
            }

            return results;
        });
    }

    public Task<string> LoadTemplateAsync(string filePath)
    {
        return Task.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return string.Empty;
            }

            try
            {
                if (!File.Exists(filePath))
                {
                    _logger?.Warn($"Template not found: {filePath}", "Templates");
                    return string.Empty;
                }

                string content = File.ReadAllText(filePath, Encoding.UTF8);

                _logger?.Info(
                    $"Loaded template: {Path.GetFileName(filePath)} ({content.Length} chars)",
                    "Templates");

                return content;
            }
            catch (Exception ex)
            {
                _logger?.Error($"Failed to load template: {filePath}", ex, "Templates");
                return string.Empty;
            }
        });
    }
}