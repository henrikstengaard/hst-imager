using Hst.Imager.Core.Commands;

namespace Hst.Imager.Core.PhysicalDrives
{
    public static class PhysicalDriveHelper
    {
        public static bool HasPhysicalDrivePaths(params string[] args)
        {
            foreach (var arg in args)
            {
                if (Regexs.DiskPathRegex.IsMatch(arg) || 
                    Regexs.PhysicalDrivePathRegex.IsMatch(arg) ||
                    Regexs.DevicePathRegex.IsMatch(arg))
                {
                    return true;
                }
            }

            return false;
        }
    }
}