using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MarkdownEditor.Helpers;

/// <summary>
/// Lightweight wrapper around Win32 file dialogs.
/// Avoids the WinUI 3 picker initialization ceremony.
/// </summary>
public static class Win32FileDialogs
{
    // ----------------------------------------------------------------------
    // Public API
    // ----------------------------------------------------------------------

    public static string? ShowOpenDialog(
        IntPtr ownerHandle,
        string title,
        string filter,
        string initialDirectory)
    {
        return ShowDialog(ownerHandle, title, filter, initialDirectory, isSave: false);
    }

    public static string? ShowSaveDialog(
        IntPtr ownerHandle,
        string title,
        string filter,
        string initialDirectory,
        string suggestedFileName)
    {
        return ShowDialog(
            ownerHandle,
            title,
            filter,
            initialDirectory,
            isSave: true,
            suggestedFileName: suggestedFileName);
    }

    // ----------------------------------------------------------------------
    // Internals
    // ----------------------------------------------------------------------

    private static string? ShowDialog(
        IntPtr ownerHandle,
        string title,
        string filter,
        string initialDirectory,
        bool isSave,
        string? suggestedFileName = null)
    {
        const int MaxPath = 32768;

        char[] fileBuffer = new char[MaxPath];

        if (!string.IsNullOrWhiteSpace(suggestedFileName))
        {
            for (int i = 0; i < suggestedFileName.Length && i < MaxPath - 1; i++)
            {
                fileBuffer[i] = suggestedFileName[i];
            }
        }

        IntPtr fileBufferPtr = Marshal.AllocHGlobal(MaxPath * sizeof(char));

        try
        {
            Marshal.Copy(fileBuffer, 0, fileBufferPtr, MaxPath);

            OPENFILENAME ofn = new OPENFILENAME
            {
                lStructSize = Marshal.SizeOf<OPENFILENAME>(),
                hwndOwner = ownerHandle,
                hInstance = IntPtr.Zero,
                lpstrFilter = ConvertFilter(filter),
                lpstrCustomFilter = IntPtr.Zero,
                nMaxCustFilter = 0,
                nFilterIndex = 1,
                lpstrFile = fileBufferPtr,
                nMaxFile = MaxPath,
                lpstrFileTitle = IntPtr.Zero,
                nMaxFileTitle = 0,
                lpstrInitialDir = initialDirectory,
                lpstrTitle = title,
                Flags = BuildFlags(isSave),
                nFileOffset = 0,
                nFileExtension = 0,
                lpstrDefExt = "md",
                lCustData = IntPtr.Zero,
                lpfnHook = IntPtr.Zero,
                lpTemplateName = IntPtr.Zero,
                pvReserved = IntPtr.Zero,
                dwReserved = 0,
                FlagsEx = 0
            };

            bool ok = isSave ? GetSaveFileName(ref ofn) : GetOpenFileName(ref ofn);

            if (!ok)
            {
                return null;
            }

            string? path = Marshal.PtrToStringUni(fileBufferPtr);
            return string.IsNullOrWhiteSpace(path) ? null : path;
        }
        finally
        {
            Marshal.FreeHGlobal(fileBufferPtr);
        }
    }

    private static int BuildFlags(bool isSave)
    {
        // OFN_EXPLORER         = 0x00080000
        // OFN_PATHMUSTEXIST    = 0x00000800
        // OFN_FILEMUSTEXIST    = 0x00001000 (open only)
        // OFN_OVERWRITEPROMPT  = 0x00000002 (save only)
        // OFN_NOCHANGEDIR      = 0x00000008
        int flags = 0x00080000 | 0x00000800 | 0x00000008;

        if (isSave)
        {
            flags |= 0x00000002;
        }
        else
        {
            flags |= 0x00001000;
        }

        return flags;
    }

    /// <summary>
    /// Converts a user-friendly filter string into the Win32 double-null format.
    /// Input  : "Markdown Files (*.md)|*.md|All Files (*.*)|*.*"
    /// Output : "Markdown Files (*.md)\0*.md\0All Files (*.*)\0*.*\0"
    /// </summary>
    private static string ConvertFilter(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return "All Files (*.*)\0*.*\0";
        }

        return filter.Replace('|', '\0') + "\0";
    }

    // ----------------------------------------------------------------------
    // Win32 interop
    // ----------------------------------------------------------------------

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OPENFILENAME
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpstrFilter;

        public IntPtr lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;

        public IntPtr lpstrFile;
        public int nMaxFile;

        public IntPtr lpstrFileTitle;
        public int nMaxFileTitle;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpstrInitialDir;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpstrTitle;

        public int Flags;
        public short nFileOffset;
        public short nFileExtension;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpstrDefExt;

        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public IntPtr lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    [DllImport("Comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetOpenFileName(ref OPENFILENAME ofn);

    [DllImport("Comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetSaveFileName(ref OPENFILENAME ofn);
}