using System;
using System.IO;

namespace MarkdownEditor.Helpers;

/// <summary>
/// Centralized resolver for all portable file paths used by the app.
/// 
/// Default behavior:
///     [AppRoot]\Data\Profiles\[Username]\Autosave
///     [AppRoot]\Data\Profiles\[Username]\Backups
///     [AppRoot]\Data\Profiles\[Username]\Settings
///     [AppRoot]\Data\Profiles\[Username]\Logs
///
/// If the application folder is read-only (network share or USB without write access),
/// falls back to:
///     %LOCALAPPDATA%\MarkdownEditor\Profiles\[Username]\...
/// </summary>
public static class AppFolders
{
    // ----------------------------------------------------------------------
    // Configuration
    // ----------------------------------------------------------------------

    private const string AppName = "MarkdownEditor";

    private const string DataFolderName = "Data";
    private const string ProfilesFolderName = "Profiles";

    private const string AutosaveFolderName = "Autosave";
    private const string BackupsFolderName = "Backups";
    private const string SettingsFolderName = "Settings";
    private const string LogsFolderName = "Logs";
    private const string TemplatesFolderName = "Templates";
    private const string AssetsFolderName = "Assets";
    private const string ConfigFolderName = "Config";

    // ----------------------------------------------------------------------
    // State
    // ----------------------------------------------------------------------

    private static bool _initialized;
    private static string _rootPath = string.Empty;
    private static string _profileRoot = string.Empty;
    private static bool _isPortable;

    // ----------------------------------------------------------------------
    // Public Properties
    // ----------------------------------------------------------------------

    /// <summary>
    /// Root directory containing the application executable.
    /// </summary>
    public static string AppRoot => AppContext.BaseDirectory;

    /// <summary>
    /// Indicates whether the app is running in portable mode (writes to AppRoot)
    /// or fallback mode (writes to %LOCALAPPDATA%).
    /// </summary>
    public static bool IsPortable
    {
        get
        {
            EnsureInitialized();
            return _isPortable;
        }
    }

    /// <summary>
    /// Effective root location for ALL app-managed user data.
    /// </summary>
    public static string DataRoot
    {
        get
        {
            EnsureInitialized();
            return _rootPath;
        }
    }

    /// <summary>
    /// Per-user profile root (supports multiple users on a shared network deployment).
    /// </summary>
    public static string ProfileRoot
    {
        get
        {
            EnsureInitialized();
            return _profileRoot;
        }
    }

    public static string Autosave => GetProfileSub(AutosaveFolderName);
    public static string Backups => GetProfileSub(BackupsFolderName);
    public static string Settings => GetProfileSub(SettingsFolderName);
    public static string Logs => GetProfileSub(LogsFolderName);

    /// <summary>
    /// Shared (read-only) folders that ship with the app.
    /// </summary>
    public static string Templates => Path.Combine(AppRoot, TemplatesFolderName);
    public static string Assets => Path.Combine(AppRoot, AssetsFolderName);
    public static string Config => Path.Combine(AppRoot, ConfigFolderName);

    // ----------------------------------------------------------------------
    // Initialization
    // ----------------------------------------------------------------------

    /// <summary>
    /// Initialize the folder resolver. Call once at application startup.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        string portableRoot = Path.Combine(AppRoot, DataFolderName);

        if (TryCreateDirectory(portableRoot))
        {
            _rootPath = portableRoot;
            _isPortable = true;
        }
        else
        {
            string fallback = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppName);

            Directory.CreateDirectory(fallback);

            _rootPath = fallback;
            _isPortable = false;
        }

        _profileRoot = Path.Combine(
            _rootPath,
            ProfilesFolderName,
            SanitizeUserName(Environment.UserName));

        EnsureProfileFolders();

        _initialized = true;
    }

    // ----------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------

    private static void EnsureInitialized()
    {
        if (!_initialized)
        {
            Initialize();
        }
    }

    private static string GetProfileSub(string name)
    {
        EnsureInitialized();

        string path = Path.Combine(_profileRoot, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void EnsureProfileFolders()
    {
        Directory.CreateDirectory(_profileRoot);
        Directory.CreateDirectory(Path.Combine(_profileRoot, AutosaveFolderName));
        Directory.CreateDirectory(Path.Combine(_profileRoot, BackupsFolderName));
        Directory.CreateDirectory(Path.Combine(_profileRoot, SettingsFolderName));
        Directory.CreateDirectory(Path.Combine(_profileRoot, LogsFolderName));
    }

    private static bool TryCreateDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);

            // Write probe to verify we actually have write permission.
            string probe = Path.Combine(path, ".writetest");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string SanitizeUserName(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return "Default";
        }

        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            userName = userName.Replace(invalid, '_');
        }

        return userName;
    }
}