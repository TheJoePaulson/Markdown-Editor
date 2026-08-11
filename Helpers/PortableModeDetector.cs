using System;
using System.IO;

namespace MarkdownEditor.Helpers;

/// <summary>
/// Provides diagnostic information about the runtime environment.
/// Useful for status bars, About dialogs, and logs.
///
/// Built on top of <see cref="AppFolders"/>.
/// </summary>
public static class PortableModeDetector
{
    // ----------------------------------------------------------------------
    // Public diagnostic snapshot
    // ----------------------------------------------------------------------

    /// <summary>
    /// Returns a snapshot of runtime environment information.
    /// </summary>
    public static RuntimeEnvironmentInfo GetEnvironment()
    {
        string root = AppFolders.AppRoot;

        DriveTypeInfo driveInfo = ResolveDriveType(root);

        bool isWritable = TestWritable(root);

        return new RuntimeEnvironmentInfo
        {
            AppRoot = root,
            DataRoot = AppFolders.DataRoot,
            ProfileRoot = AppFolders.ProfileRoot,
            IsPortable = AppFolders.IsPortable,
            IsAppFolderWritable = isWritable,
            DriveLetter = driveInfo.DriveLetter,
            DriveKind = driveInfo.Kind,
            DriveFormat = driveInfo.Format,
            DriveLabel = driveInfo.Label,
            UserName = Environment.UserName,
            MachineName = Environment.MachineName,
            OSVersion = Environment.OSVersion.VersionString,
            ProcessArchitecture = System.Runtime.InteropServices.RuntimeInformation
                .ProcessArchitecture.ToString()
        };
    }

    // ----------------------------------------------------------------------
    // Convenience checks
    // ----------------------------------------------------------------------

    public static bool IsRunningFromNetwork()
    {
        return ResolveDriveType(AppFolders.AppRoot).Kind == DriveKind.Network;
    }

    public static bool IsRunningFromUsb()
    {
        return ResolveDriveType(AppFolders.AppRoot).Kind == DriveKind.Removable;
    }

    public static bool IsRunningFromLocalDisk()
    {
        return ResolveDriveType(AppFolders.AppRoot).Kind == DriveKind.LocalDisk;
    }

    // ----------------------------------------------------------------------
    // Internals
    // ----------------------------------------------------------------------

    private static DriveTypeInfo ResolveDriveType(string path)
    {
        DriveTypeInfo result = new DriveTypeInfo
        {
            DriveLetter = string.Empty,
            Kind = DriveKind.Unknown,
            Format = string.Empty,
            Label = string.Empty
        };

        if (string.IsNullOrWhiteSpace(path))
        {
            return result;
        }

        try
        {
            // UNC path detection: \\server\share\...
            if (path.StartsWith(@"\\", StringComparison.Ordinal))
            {
                result.Kind = DriveKind.Network;
                result.DriveLetter = "(UNC)";
                return result;
            }

            string? root = Path.GetPathRoot(path);

            if (string.IsNullOrWhiteSpace(root))
            {
                return result;
            }

            DriveInfo drive = new DriveInfo(root);

            result.DriveLetter = drive.Name.TrimEnd('\\');

            result.Kind = drive.DriveType switch
            {
                DriveType.Removable => DriveKind.Removable,
                DriveType.Fixed => DriveKind.LocalDisk,
                DriveType.Network => DriveKind.Network,
                DriveType.CDRom => DriveKind.Optical,
                DriveType.Ram => DriveKind.Ram,
                _ => DriveKind.Unknown
            };

            if (drive.IsReady)
            {
                result.Format = drive.DriveFormat;
                result.Label = drive.VolumeLabel;
            }
        }
        catch
        {
            // Drive info can throw on restricted environments.
        }

        return result;
    }

    private static bool TestWritable(string path)
    {
        try
        {
            string probe = Path.Combine(path, ".writetest.tmp");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

// --------------------------------------------------------------------------
// Supporting types
// --------------------------------------------------------------------------

public enum DriveKind
{
    Unknown,
    LocalDisk,
    Removable,
    Network,
    Optical,
    Ram
}

public sealed class DriveTypeInfo
{
    public string DriveLetter { get; set; } = string.Empty;
    public DriveKind Kind { get; set; }
    public string Format { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public sealed class RuntimeEnvironmentInfo
{
    public string AppRoot { get; set; } = string.Empty;
    public string DataRoot { get; set; } = string.Empty;
    public string ProfileRoot { get; set; } = string.Empty;

    public bool IsPortable { get; set; }
    public bool IsAppFolderWritable { get; set; }

    public string DriveLetter { get; set; } = string.Empty;
    public DriveKind DriveKind { get; set; }
    public string DriveFormat { get; set; } = string.Empty;
    public string DriveLabel { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string OSVersion { get; set; } = string.Empty;
    public string ProcessArchitecture { get; set; } = string.Empty;

    /// <summary>
    /// Returns a human-readable string suitable for status bars or logs.
    /// </summary>
    public override string ToString()
    {
        string mode = IsPortable ? "Portable" : "Fallback (LOCALAPPDATA)";
        string drive = string.IsNullOrEmpty(DriveLetter)
            ? "Unknown drive"
            : $"{DriveLetter} [{DriveKind}]";

        return $"{mode} | {drive} | User: {UserName} | OS: {OSVersion} | Arch: {ProcessArchitecture}";
    }
}