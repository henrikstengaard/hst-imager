using Hst.Amiga.FileSystems.Pfs3;

namespace Hst.Imager.Core.Models
{
    public class Settings
    {
        public enum MacOsElevateMethodEnum
        {
            OsascriptAdministrator,
            OsascriptSudo
        }

        public enum ThemeEnum
        {
            Amiga,
            Light,
            Windows,
            MacOs,
            Linux
        }
        
        public bool AllPhysicalDrives { get; set; }

        public MacOsElevateMethodEnum MacOsElevateMethod { get; set; }

        public bool DebugMode { get; set; }
        //public ThemeEnum Theme { get; set; }

        public bool Verify { get; set; }
        public bool Force { get; set; }
        public int Retries { get; set; }
        
        /// <summary>
        /// Skip unused sectors.
        /// </summary>
        public bool SkipUnusedSectors { get; set; }
        
        /// <summary>
        /// Use cache for physical drive operations.
        /// </summary>
        public bool UseCache { get; set; }

        /// <summary>
        /// Type of cache to use.
        /// </summary>
        public CacheType CacheType { get; set; }

        public Settings()
        {
            AllPhysicalDrives = false;
            MacOsElevateMethod = MacOsElevateMethodEnum.OsascriptSudo;
            DebugMode = false;
            Verify = false;
            Force = false;
            Retries = 5;
            SkipUnusedSectors = false;
            UseCache = true;
            CacheType = CacheType.Disk;
        }
    }
}