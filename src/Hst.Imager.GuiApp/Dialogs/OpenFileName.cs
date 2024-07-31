using System;
using System.Runtime.InteropServices;

namespace Hst.Imager.GuiApp.Dialogs;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public class OpenFileName
{
    public int structSize = 0;
    public IntPtr dlgOwner = IntPtr.Zero;
    public IntPtr instance = IntPtr.Zero;
    public string filter;
    public string customFilter;
    public int maxCustFilter = 0;
    public int filterIndex = 0;
    public IntPtr file;
    public int maxFile = 0;
    public string fileTitle;
    public int maxFileTitle = 0;
    public string initialDir;
    public string title;
    public int flags = 0;
    public short fileOffset = 0;
    public short fileExtension = 0;
    public string defExt;
    public IntPtr custData = IntPtr.Zero;
    public IntPtr hook = IntPtr.Zero;
    public string templateName;
    public IntPtr reservedPtr = IntPtr.Zero;
    public int reservedInt = 0;
    public int flagsEx = 0;
}