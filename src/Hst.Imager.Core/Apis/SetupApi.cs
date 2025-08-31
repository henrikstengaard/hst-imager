using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Hst.Imager.Core.Apis;

/// <summary>
/// PInvoke definitions for the SetupAPI functions used to enumerate and interact with device interfaces.
/// </summary>
public static class SetupApi
{
    public static readonly Guid GUID_DEVINTERFACE_DISK = new Guid("53F56307-B6BF-11D0-94F2-00A0C91EFB8B");
    public const int ERROR_NO_MORE_ITEMS = 259;
    public const int ERROR_INSUFFICIENT_BUFFER = 122;
    public const int ERROR_INVALID_DATA = 13;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct SP_DEVICE_INTERFACE_DETAIL_DATA
    {
        public int cbSize;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string DevicePath;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SP_DEVINFO_DATA
    {
        public int cbSize; // Size of the structure in bytes
        public Guid ClassGuid; // GUID of the device's setup class
        public uint DevInst; // Handle to the device instance
        public IntPtr Reserved; // Reserved, must be zero
    }

    // Define the SP_DEVICE_INTERFACE_DATA structure
    [StructLayout(LayoutKind.Sequential)]
    public struct SP_DEVICE_INTERFACE_DATA
    {
        public int cbSize; // Size of the structure in bytes
        public Guid InterfaceClassGuid; // GUID for the device interface class
        public int Flags; // Flags that provide information about the interface
        public IntPtr Reserved; // Reserved for internal use by the operating system
    }

    [Flags]
    public enum DiGetClassFlags : uint
    {
        DIGCF_DEFAULT = 0x00000001, // only valid with DIGCF_DEVICEINTERFACE
        DIGCF_PRESENT = 0x00000002,
        DIGCF_ALLCLASSES = 0x00000004,
        DIGCF_PROFILE = 0x00000008,
        DIGCF_DEVICEINTERFACE = 0x00000010,
    }

    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern IntPtr SetupDiGetClassDevs(
        ref Guid ClassGuid,
        IntPtr Enumerator,
        IntPtr hwndParent,
        int Flags);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto )]
    public static extern bool SetupDiEnumDeviceInterfaces(
        IntPtr DeviceInfoSet,
        IntPtr DeviceInfoData,
        ref Guid InterfaceClassGuid,
        int MemberIndex,
        ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);

    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiGetDeviceInterfaceDetail(
        IntPtr DeviceInfoSet,
        ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData,
        IntPtr DeviceInterfaceDetailData,
        int DeviceInterfaceDetailDataSize,
        out int RequiredSize,
        IntPtr DeviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiGetDeviceInterfaceDetail(
        IntPtr DeviceInfoSet,
        ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData,
        ref SP_DEVICE_INTERFACE_DETAIL_DATA DeviceInterfaceDetailData,
        int DeviceInterfaceDetailDataSize,
        out int RequiredSize,
        ref SP_DEVINFO_DATA DeviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiGetDeviceInterfaceDetail(
        IntPtr DeviceInfoSet,
        ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData,
        IntPtr DeviceInterfaceDetailData,
        uint DeviceInterfaceDetailDataSize,
        out int RequiredSize,
        ref SP_DEVINFO_DATA DeviceInfoData);
  
    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);
    
    [DllImport( "setupapi.dll" )]
    public static extern int CM_Get_Parent(
        ref int pdnDevInst,
        int dnDevInst,
        int ulFlags );

    [DllImport( "setupapi.dll" )]
    public static extern int CM_Request_Device_Eject(
        int dnDevInst,
        out PNP_VETO_TYPE pVetoType,
        StringBuilder pszVetoName,
        int ulNameLength,
        int ulFlags );

    [DllImport( "setupapi.dll", EntryPoint = "CM_Request_Device_Eject" )]
    public static extern int CM_Request_Device_Eject_NoUi(
        int dnDevInst,
        IntPtr pVetoType,
        StringBuilder pszVetoName,
        int ulNameLength,
        int ulFlags );

    public enum PNP_VETO_TYPE
    {
        Ok,
        TypeUnknown,
        LegacyDevice,
        PendingClose,
        WindowsApp,
        WindowsService,
        OutstandingOpen,
        Device,
        Driver,
        IllegalDeviceRequest,
        InsufficientPower,
        NonDisableable,
        LegacyDriver,
        InsufficientRights
    }
}