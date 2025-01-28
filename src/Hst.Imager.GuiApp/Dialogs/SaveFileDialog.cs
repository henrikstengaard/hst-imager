using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace Hst.Imager.GuiApp.Dialogs;

/// <summary>
/// P/Invoke SaveFileDialog (no dependency on System.Windows.Forms)
/// https://gist.github.com/gotmachine/4ffaf7837f9fbb0ab4a648979ee40609
/// </summary>
public class SaveFileDialog
{
    [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool GetSaveFileName([In, Out] OpenFileName ofn);

    public string Title { get; set; } = "Save a file...";
    public string InitialDirectory { get; set; } = null;
    public string Filter { get; set; } = "All files(*.*)\0\0";
    public bool ShowHidden { get; set; } = false;
    public bool Success { get; private set; }
    public string[] Files { get; private set; }

    /// <summary>
    /// Save a single file
    /// </summary>
    /// <param name="file">Path to the selected file, or null if the return value is false</param>
    /// <param name="title">Title of the dialog</param>
    /// <param name="filter">File name filter. Example : "txt files (*.txt)|*.txt|All files (*.*)|*.*"</param>
    /// <param name="initialDirectory">Example : "c:\\"</param>
    /// <param name="showHidden">Forces the showing of system and hidden files</param>
    /// <returns>True of a file was selected, false if the dialog was cancelled or closed</returns>
    public static bool SaveFile(out string file, string title = null, string filter = null,
        string initialDirectory = null, bool showHidden = false)
    {
        var dialog = new SaveFileDialog();
        dialog.Title = title;
        dialog.InitialDirectory = initialDirectory;
        dialog.Filter = filter;
        dialog.ShowHidden = showHidden;

        dialog.ShowDialog();
        if (dialog.Success)
        {
            file = dialog.Files[0];
            return true;
        }

        file = null;
        return false;
    }

    private void ShowDialog()
    {
        Thread thread = new Thread(ShowSaveFileDialog);
#pragma warning disable CA1416
        thread.SetApartmentState(ApartmentState.STA);
#pragma warning restore CA1416
        thread.Start();
        thread.Join();
    }

    private void ShowSaveFileDialog()
    {
        const int maxFileLength = 2048;

        Success = false;
        Files = null;

        OpenFileName ofn = new OpenFileName();

        ofn.structSize = Marshal.SizeOf(ofn);
        ofn.filter = Filter?.Replace("|", "\0") + "\0";
        ofn.fileTitle = new string(new char[maxFileLength]);
        ofn.maxFileTitle = ofn.fileTitle.Length;
        ofn.initialDir = InitialDirectory;
        ofn.title = Title;
        ofn.flags = (int)OpenFileNameFlags.OFN_HIDEREADONLY | (int)OpenFileNameFlags.OFN_EXPLORER |
            (int)OpenFileNameFlags.OFN_OVERWRITEPROMPT;

        // Create buffer for file names
        ofn.file = Marshal.AllocHGlobal(maxFileLength * Marshal.SystemDefaultCharSize);
        ofn.maxFile = maxFileLength;

        // Initialize buffer with NULL bytes
        for (int i = 0; i < maxFileLength * Marshal.SystemDefaultCharSize; i++)
        {
            Marshal.WriteByte(ofn.file, i, 0);
        }

        if (ShowHidden)
        {
            ofn.flags |= (int)OpenFileNameFlags.OFN_FORCESHOWHIDDEN;
        }

        Success = GetSaveFileName(ofn);

        if (Success)
        {
            IntPtr filePointer = ofn.file;
            long pointer = (long)filePointer;
            string file = Marshal.PtrToStringAuto(filePointer);
            List<string> strList = new List<string>();

            // Retrieve file names
            while (file.Length > 0)
            {
                strList.Add(file);

                pointer += file.Length * Marshal.SystemDefaultCharSize + Marshal.SystemDefaultCharSize;
                filePointer = (IntPtr)pointer;
                file = Marshal.PtrToStringAuto(filePointer);
            }

            if (strList.Count > 1)
            {
                Files = new string[strList.Count - 1];
                for (int i = 1; i < strList.Count; i++)
                {
                    Files[i - 1] = Path.Combine(strList[0], strList[i]);
                }
            }
            else
            {
                Files = strList.ToArray();
            }
        }

        Marshal.FreeHGlobal(ofn.file);
    }
}