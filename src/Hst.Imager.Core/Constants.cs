using Hst.Imager.Core.Models;

namespace Hst.Imager.Core
{
    public static class Constants
    {
        public static class BiosPartitionTypes
        {
            public const byte PiStormRdb = 0x76;
        }

        public static class FileSystemNames
        {
            public const string PiStormRdb = "PiStorm RDB";
        }

        public static class EntryPropertyNames
        {
            public const string Comment = "Comment";
            public const string ProtectionBits = "$ProtectionBits";
        }
    }
}