namespace Hst.Imager.GuiApp.Dialogs;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

// public class WindowsDialog
// {
//     // From https://www.pinvoke.net/default.aspx/Structures/OPENFILENAME.html
//     [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
//     public struct OpenFileName
//     {
//         public int lStructSize;
//         public IntPtr hwndOwner;
//         public IntPtr hInstance;
//         public string lpstrFilter;
//         public string lpstrCustomFilter;
//         public int nMaxCustFilter;
//         public int nFilterIndex;
//         public string lpstrFile;
//         public int nMaxFile;
//         public string lpstrFileTitle;
//         public int nMaxFileTitle;
//         public string lpstrInitialDir;
//         public string lpstrTitle;
//         public int Flags;
//         public short nFileOffset;
//         public short nFileExtension;
//         public string lpstrDefExt;
//         public IntPtr lCustData;
//         public IntPtr lpfnHook;
//         public string lpTemplateName;
//         public IntPtr pvReserved;
//         public int dwReserved;
//         public int flagsEx;
//     }
//     
//     // From https://www.pinvoke.net/default.aspx/comdlg32/GetOpenFileName.html
//     [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
//     private static extern bool GetOpenFileName(ref OpenFileName ofn);
//
//     [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
//     private static extern bool GetSaveFileName(ref OpenFileName ofn);
//
//     /// <summary>
//     /// Show open file dialog
//     /// </summary>
//     /// <param name="title">Title</param>
//     /// <param name="filter">List of filters, eg. "Text Files (*.txt)|*.txt|All Files (*.*)|*.*"</param>
//     /// <returns></returns>
//     public static string ShowOpenDialog(string title, string filter)
//     {
//         // create open file name struct
//         var openFileName = new OpenFileName();
//         
//         openFileName.lStructSize = Marshal.SizeOf(openFileName);
//         openFileName.lpstrFile = new string(new char[256]);
//         openFileName.nMaxFile = openFileName.lpstrFile.Length;
//         openFileName.lpstrFileTitle = new string(new char[64]);
//         openFileName.nMaxFileTitle = openFileName.lpstrFileTitle.Length;
//         
//         // flags: path must exist
//         openFileName.Flags = 0x00000800;
//
//         openFileName.lpstrFilter = string.Concat(filter.Replace("|", "\0"), "\0");
//         openFileName.lpstrTitle = title.Length > 64 ? title.Substring(0, 64) : title;
//
//         return GetOpenFileName(ref openFileName) ? openFileName.lpstrFile : null;
//     }
//
//     public static string ShowSaveDialog(string title, string filter)
//     {
//         // create open file name struct
//         var openFileName = new OpenFileName();
//         
//         openFileName.lStructSize = Marshal.SizeOf(openFileName);
//         openFileName.lpstrFile = new string(new char[256]);
//         openFileName.nMaxFile = openFileName.lpstrFile.Length;
//         openFileName.lpstrFileTitle = new string(new char[64]);
//         openFileName.nMaxFileTitle = openFileName.lpstrFileTitle.Length;
//         openFileName.lpstrInitialDir = "c:\\\0";
//
//         // flags: prompt overwrite
//         openFileName.Flags = 0x00000002;
//         
//         openFileName.lpstrFilter = string.Concat(filter.Replace("|", "\0"), "\0");
//         openFileName.lpstrTitle = title.Length > 64 ? title.Substring(0, 64) : title;
//
//         return GetSaveFileName(ref openFileName) ? openFileName.lpstrFile : null;
//     }
//     
//     public static string ShowFolderDialog(string title = null, string initialPath = null)
//     {
//         return ShowFolderDialog(IntPtr.Zero, title: title, initialPath: initialPath);
//     }
//     
//     public static string ShowFolderDialog(IntPtr owner, string title = null, string initialPath = null, bool throwOnError = false)
//     {
//         var dialog = (IFileOpenDialog)new FileOpenDialog();
//         if (!string.IsNullOrEmpty(initialPath))
//         {
//             if (CheckHr(SHCreateItemFromParsingName(initialPath, null, typeof(IShellItem).GUID, out var item), throwOnError) != 0)
//                 return null;
//
//             dialog.SetFolder(item);
//         }
//
//         var options = FOS.FOS_PICKFOLDERS;
//         options = (FOS)SetOptions((int)options, false, false);
//         dialog.SetOptions(options);
//
//         if (title != null)
//         {
//             dialog.SetTitle(title);
//         }
//
//         // if (OkButtonLabel != null)
//         // {
//         //     dialog.SetOkButtonLabel(OkButtonLabel);
//         // }
//
//         // if (FileNameLabel != null)
//         // {
//         //     dialog.SetFileName(FileNameLabel);
//         // }
//
//         if (owner == IntPtr.Zero)
//         {
//             owner = Process.GetCurrentProcess().MainWindowHandle;
//             if (owner == IntPtr.Zero)
//             {
//                 owner = GetDesktopWindow();
//             }
//         }
//
//         var hr = dialog.Show(owner);
//         if (hr == ERROR_CANCELLED)
//             return null;
//
//         if (CheckHr(hr, throwOnError) != 0)
//             return null;
//
//         if (CheckHr(dialog.GetResults(out var items), throwOnError) != 0)
//             return null;
//
//         var paths = new List<string>();
//         items.GetCount(out var count);
//         for (var i = 0; i < count; i++)
//         {
//             items.GetItemAt(i, out var item);
//             CheckHr(item.GetDisplayName(SIGDN.SIGDN_DESKTOPABSOLUTEPARSING, out var path), throwOnError);
//             CheckHr(item.GetDisplayName(SIGDN.SIGDN_DESKTOPABSOLUTEEDITING, out var name), throwOnError);
//             if (path != null || name != null)
//             {
//                 paths.Add(path);
//                 //_resultNames.Add(name);
//             }
//         }
//         
//         return paths.Any() ? paths[0] : null;
//     }
//
//     private static int SetOptions(int options, bool forceFileSystem, bool multiselect)
//     {
//         if (forceFileSystem)
//         {
//             options |= (int)FOS.FOS_FORCEFILESYSTEM;
//         }
//
//         if (multiselect)
//         {
//             options |= (int)FOS.FOS_ALLOWMULTISELECT;
//         }
//         
//         return options;
//     }
//     
//     private static int CheckHr(int hr, bool throwOnError)
//     {
//         if (hr != 0 && throwOnError) Marshal.ThrowExceptionForHR(hr);
//         return hr;
//     }
//
//     [DllImport("shell32")]
//     private static extern int SHCreateItemFromParsingName([MarshalAs(UnmanagedType.LPWStr)] string pszPath, IBindCtx pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IShellItem ppv);
//
//     [DllImport("user32")]
//     private static extern IntPtr GetDesktopWindow();
//
// #pragma warning disable IDE1006 // Naming Styles
//     private const int ERROR_CANCELLED = unchecked((int)0x800704C7);
// #pragma warning restore IDE1006 // Naming Styles
//
//     [ComImport, Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")] // CLSID_FileOpenDialog
//     private class FileOpenDialog { }
//
//     [ComImport, Guid("d57c7288-d4ad-4768-be02-9d969532d960"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
//     private interface IFileOpenDialog
//     {
//         [PreserveSig] int Show(IntPtr parent); // IModalWindow
//         [PreserveSig] int SetFileTypes();  // not fully defined
//         [PreserveSig] int SetFileTypeIndex(int iFileType);
//         [PreserveSig] int GetFileTypeIndex(out int piFileType);
//         [PreserveSig] int Advise(); // not fully defined
//         [PreserveSig] int Unadvise();
//         [PreserveSig] int SetOptions(FOS fos);
//         [PreserveSig] int GetOptions(out FOS pfos);
//         [PreserveSig] int SetDefaultFolder(IShellItem psi);
//         [PreserveSig] int SetFolder(IShellItem psi);
//         [PreserveSig] int GetFolder(out IShellItem ppsi);
//         [PreserveSig] int GetCurrentSelection(out IShellItem ppsi);
//         [PreserveSig] int SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
//         [PreserveSig] int GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
//         [PreserveSig] int SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
//         [PreserveSig] int SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
//         [PreserveSig] int SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
//         [PreserveSig] int GetResult(out IShellItem ppsi);
//         [PreserveSig] int AddPlace(IShellItem psi, int alignment);
//         [PreserveSig] int SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
//         [PreserveSig] int Close(int hr);
//         [PreserveSig] int SetClientGuid();  // not fully defined
//         [PreserveSig] int ClearClientData();
//         [PreserveSig] int SetFilter([MarshalAs(UnmanagedType.IUnknown)] object pFilter);
//         [PreserveSig] int GetResults(out IShellItemArray ppenum);
//         [PreserveSig] int GetSelectedItems([MarshalAs(UnmanagedType.IUnknown)] out object ppsai);
//     }
//
//     [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
//     private interface IShellItem
//     {
//         [PreserveSig] int BindToHandler(); // not fully defined
//         [PreserveSig] int GetParent(); // not fully defined
//         [PreserveSig] int GetDisplayName(SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
//         [PreserveSig] int GetAttributes();  // not fully defined
//         [PreserveSig] int Compare();  // not fully defined
//     }
//
//     [ComImport, Guid("b63ea76d-1f85-456f-a19c-48159efa858b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
//     private interface IShellItemArray
//     {
//         [PreserveSig] int BindToHandler();  // not fully defined
//         [PreserveSig] int GetPropertyStore();  // not fully defined
//         [PreserveSig] int GetPropertyDescriptionList();  // not fully defined
//         [PreserveSig] int GetAttributes();  // not fully defined
//         [PreserveSig] int GetCount(out int pdwNumItems);
//         [PreserveSig] int GetItemAt(int dwIndex, out IShellItem ppsi);
//         [PreserveSig] int EnumItems();  // not fully defined
//     }
//
// #pragma warning disable CA1712 // Do not prefix enum values with type name
//     private enum SIGDN : uint
//     {
//         SIGDN_DESKTOPABSOLUTEEDITING = 0x8004c000,
//         SIGDN_DESKTOPABSOLUTEPARSING = 0x80028000,
//         SIGDN_FILESYSPATH = 0x80058000,
//         SIGDN_NORMALDISPLAY = 0,
//         SIGDN_PARENTRELATIVE = 0x80080001,
//         SIGDN_PARENTRELATIVEEDITING = 0x80031001,
//         SIGDN_PARENTRELATIVEFORADDRESSBAR = 0x8007c001,
//         SIGDN_PARENTRELATIVEPARSING = 0x80018001,
//         SIGDN_URL = 0x80068000
//     }
//
//     [Flags]
//     private enum FOS
//     {
//         FOS_OVERWRITEPROMPT = 0x2,
//         FOS_STRICTFILETYPES = 0x4,
//         FOS_NOCHANGEDIR = 0x8,
//         FOS_PICKFOLDERS = 0x20,
//         FOS_FORCEFILESYSTEM = 0x40,
//         FOS_ALLNONSTORAGEITEMS = 0x80,
//         FOS_NOVALIDATE = 0x100,
//         FOS_ALLOWMULTISELECT = 0x200,
//         FOS_PATHMUSTEXIST = 0x800,
//         FOS_FILEMUSTEXIST = 0x1000,
//         FOS_CREATEPROMPT = 0x2000,
//         FOS_SHAREAWARE = 0x4000,
//         FOS_NOREADONLYRETURN = 0x8000,
//         FOS_NOTESTFILECREATE = 0x10000,
//         FOS_HIDEMRUPLACES = 0x20000,
//         FOS_HIDEPINNEDPLACES = 0x40000,
//         FOS_NODEREFERENCELINKS = 0x100000,
//         FOS_OKBUTTONNEEDSINTERACTION = 0x200000,
//         FOS_DONTADDTORECENT = 0x2000000,
//         FOS_FORCESHOWHIDDEN = 0x10000000,
//         FOS_DEFAULTNOMINIMODE = 0x20000000,
//         FOS_FORCEPREVIEWPANEON = 0x40000000,
//         FOS_SUPPORTSTREAMABLEITEMS = unchecked((int)0x80000000)
//     }
// #pragma warning restore CA1712 // Do not prefix enum values with type name
// }

public class FolderPicker
{
    // reference: https://stackoverflow.com/questions/11624298/how-do-i-use-openfiledialog-to-select-a-folder
    private readonly List<string> _resultPaths = new List<string>();
    private readonly List<string> _resultNames = new List<string>();

    public IReadOnlyList<string> ResultPaths => _resultPaths;
    public IReadOnlyList<string> ResultNames => _resultNames;
    public string ResultPath => ResultPaths.FirstOrDefault();
    public string ResultName => ResultNames.FirstOrDefault();
    public virtual string InputPath { get; set; }
    public virtual bool ForceFileSystem { get; set; }
    public virtual bool Multiselect { get; set; }
    public virtual string Title { get; set; }
    public virtual string OkButtonLabel { get; set; }
    public virtual string FileNameLabel { get; set; }

    protected virtual int SetOptions(int options)
    {
        if (ForceFileSystem)
        {
            options |= (int)FOS.FOS_FORCEFILESYSTEM;
        }

        if (Multiselect)
        {
            options |= (int)FOS.FOS_ALLOWMULTISELECT;
        }
        return options;
    }

    // WPF support
    // public bool? ShowDialog(Window owner = null, bool throwOnError = false)
    // {
    //     owner = owner ?? Application.Current?.MainWindow;
    //     return ShowDialog(owner != null ? new WindowInteropHelper(owner).Handle : IntPtr.Zero, throwOnError);
    // }

    public virtual bool? ShowDialog(IntPtr owner, bool throwOnError = false)
    {
        var dialog = (IFileOpenDialog)new FileOpenDialog();
        if (!string.IsNullOrEmpty(InputPath))
        {
            if (CheckHr(SHCreateItemFromParsingName(InputPath, null, typeof(IShellItem).GUID, out var item), throwOnError) != 0)
                return null;

            dialog.SetFolder(item);
        }

        var options = FOS.FOS_PICKFOLDERS;
        options = (FOS)SetOptions((int)options);
        dialog.SetOptions(options);

        if (Title != null)
        {
            dialog.SetTitle(Title);
        }

        if (OkButtonLabel != null)
        {
            dialog.SetOkButtonLabel(OkButtonLabel);
        }

        if (FileNameLabel != null)
        {
            dialog.SetFileName(FileNameLabel);
        }

        if (owner == IntPtr.Zero)
        {
            owner = Process.GetCurrentProcess().MainWindowHandle;
            if (owner == IntPtr.Zero)
            {
                owner = GetDesktopWindow();
            }
        }

        var hr = dialog.Show(owner);
        if (hr == ERROR_CANCELLED)
            return null;

        if (CheckHr(hr, throwOnError) != 0)
            return null;

        if (CheckHr(dialog.GetResults(out var items), throwOnError) != 0)
            return null;

        items.GetCount(out var count);
        for (var i = 0; i < count; i++)
        {
            items.GetItemAt(i, out var item);
            CheckHr(item.GetDisplayName(SIGDN.SIGDN_DESKTOPABSOLUTEPARSING, out var path), throwOnError);
            CheckHr(item.GetDisplayName(SIGDN.SIGDN_DESKTOPABSOLUTEEDITING, out var name), throwOnError);
            if (path != null || name != null)
            {
                _resultPaths.Add(path);
                _resultNames.Add(name);
            }
        }
        return true;
    }

    private static int CheckHr(int hr, bool throwOnError)
    {
        if (hr != 0 && throwOnError) Marshal.ThrowExceptionForHR(hr);
        return hr;
    }

    [DllImport("shell32")]
    private static extern int SHCreateItemFromParsingName([MarshalAs(UnmanagedType.LPWStr)] string pszPath, IBindCtx pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IShellItem ppv);

    [DllImport("user32")]
    private static extern IntPtr GetDesktopWindow();

#pragma warning disable IDE1006 // Naming Styles
    private const int ERROR_CANCELLED = unchecked((int)0x800704C7);
#pragma warning restore IDE1006 // Naming Styles

    [ComImport, Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")] // CLSID_FileOpenDialog
    private class FileOpenDialog { }

    [ComImport, Guid("d57c7288-d4ad-4768-be02-9d969532d960"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialog
    {
        [PreserveSig] int Show(IntPtr parent); // IModalWindow
        [PreserveSig] int SetFileTypes();  // not fully defined
        [PreserveSig] int SetFileTypeIndex(int iFileType);
        [PreserveSig] int GetFileTypeIndex(out int piFileType);
        [PreserveSig] int Advise(); // not fully defined
        [PreserveSig] int Unadvise();
        [PreserveSig] int SetOptions(FOS fos);
        [PreserveSig] int GetOptions(out FOS pfos);
        [PreserveSig] int SetDefaultFolder(IShellItem psi);
        [PreserveSig] int SetFolder(IShellItem psi);
        [PreserveSig] int GetFolder(out IShellItem ppsi);
        [PreserveSig] int GetCurrentSelection(out IShellItem ppsi);
        [PreserveSig] int SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        [PreserveSig] int GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
        [PreserveSig] int SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        [PreserveSig] int SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
        [PreserveSig] int SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        [PreserveSig] int GetResult(out IShellItem ppsi);
        [PreserveSig] int AddPlace(IShellItem psi, int alignment);
        [PreserveSig] int SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        [PreserveSig] int Close(int hr);
        [PreserveSig] int SetClientGuid();  // not fully defined
        [PreserveSig] int ClearClientData();
        [PreserveSig] int SetFilter([MarshalAs(UnmanagedType.IUnknown)] object pFilter);
        [PreserveSig] int GetResults(out IShellItemArray ppenum);
        [PreserveSig] int GetSelectedItems([MarshalAs(UnmanagedType.IUnknown)] out object ppsai);
    }

    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        [PreserveSig] int BindToHandler(); // not fully defined
        [PreserveSig] int GetParent(); // not fully defined
        [PreserveSig] int GetDisplayName(SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        [PreserveSig] int GetAttributes();  // not fully defined
        [PreserveSig] int Compare();  // not fully defined
    }

    [ComImport, Guid("b63ea76d-1f85-456f-a19c-48159efa858b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemArray
    {
        [PreserveSig] int BindToHandler();  // not fully defined
        [PreserveSig] int GetPropertyStore();  // not fully defined
        [PreserveSig] int GetPropertyDescriptionList();  // not fully defined
        [PreserveSig] int GetAttributes();  // not fully defined
        [PreserveSig] int GetCount(out int pdwNumItems);
        [PreserveSig] int GetItemAt(int dwIndex, out IShellItem ppsi);
        [PreserveSig] int EnumItems();  // not fully defined
    }

#pragma warning disable CA1712 // Do not prefix enum values with type name
    private enum SIGDN : uint
    {
        SIGDN_DESKTOPABSOLUTEEDITING = 0x8004c000,
        SIGDN_DESKTOPABSOLUTEPARSING = 0x80028000,
        SIGDN_FILESYSPATH = 0x80058000,
        SIGDN_NORMALDISPLAY = 0,
        SIGDN_PARENTRELATIVE = 0x80080001,
        SIGDN_PARENTRELATIVEEDITING = 0x80031001,
        SIGDN_PARENTRELATIVEFORADDRESSBAR = 0x8007c001,
        SIGDN_PARENTRELATIVEPARSING = 0x80018001,
        SIGDN_URL = 0x80068000
    }

    [Flags]
    private enum FOS
    {
        FOS_OVERWRITEPROMPT = 0x2,
        FOS_STRICTFILETYPES = 0x4,
        FOS_NOCHANGEDIR = 0x8,
        FOS_PICKFOLDERS = 0x20,
        FOS_FORCEFILESYSTEM = 0x40,
        FOS_ALLNONSTORAGEITEMS = 0x80,
        FOS_NOVALIDATE = 0x100,
        FOS_ALLOWMULTISELECT = 0x200,
        FOS_PATHMUSTEXIST = 0x800,
        FOS_FILEMUSTEXIST = 0x1000,
        FOS_CREATEPROMPT = 0x2000,
        FOS_SHAREAWARE = 0x4000,
        FOS_NOREADONLYRETURN = 0x8000,
        FOS_NOTESTFILECREATE = 0x10000,
        FOS_HIDEMRUPLACES = 0x20000,
        FOS_HIDEPINNEDPLACES = 0x40000,
        FOS_NODEREFERENCELINKS = 0x100000,
        FOS_OKBUTTONNEEDSINTERACTION = 0x200000,
        FOS_DONTADDTORECENT = 0x2000000,
        FOS_FORCESHOWHIDDEN = 0x10000000,
        FOS_DEFAULTNOMINIMODE = 0x20000000,
        FOS_FORCEPREVIEWPANEON = 0x40000000,
        FOS_SUPPORTSTREAMABLEITEMS = unchecked((int)0x80000000)
    }
#pragma warning restore CA1712 // Do not prefix enum values with type name
}