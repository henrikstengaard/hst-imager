using System;

namespace Hst.Imager.Core
{
    public static class Constants
    {
        public static class BiosPartitionTypes
        {
            public const byte PiStormRdb = 0x76;
        }

        public static class GuidPartitionTypes
        {
            public static readonly Guid PiStormRdb = new("3F82EEBC-87C9-4097-8165-89D6540557C0");
        }

        public static class FileSystemNames
        {
            public const string PiStormRdb = "PiStorm RDB";
        }

        public static class EntryPropertyNames
        {
            public const string Comment = "Comment";
            public const string Link = "Link";
            public const string ProtectionBits = "$ProtectionBits";
            public const string WindowsAttributes = "$WindowsAttributes";
            public const string UnixFileMode = "$UnixFileMode";
        }
    }
}