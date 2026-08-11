using System.Collections.Generic;
using System.Threading.Tasks;

using MarkdownEditor.Models;

namespace MarkdownEditor.Services;

/// <summary>
/// Contract for the autosave service.
/// Saves and restores draft snapshots of documents to disk.
/// </summary>
public interface IAutosaveService
{
    /// <summary>
    /// Schedules an autosave for the given document.
    /// Multiple schedule calls within the debounce window collapse into one write.
    /// </summary>
    void ScheduleAutosave(MarkdownDocument document);

    /// <summary>
    /// Forces an immediate, synchronous save of the given document.
    /// </summary>
    Task ForceSaveAsync(MarkdownDocument document);

    /// <summary>
    /// Loads all currently persisted drafts from the autosave folder.
    /// </summary>
    Task<IReadOnlyList<MarkdownDocumentSnapshot>> LoadDraftsAsync();

    /// <summary>
    /// Deletes the persisted draft for a specific document.
    /// </summary>
    Task ClearDraftAsync(MarkdownDocument document);

    /// <summary>
    /// Deletes all drafts in the autosave folder.
    /// </summary>
    Task ClearAllDraftsAsync();

    /// <summary>
    /// Returns the absolute path of the autosave folder.
    /// </summary>
    string AutosaveFolderPath { get; }
}