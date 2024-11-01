using System.IO;
using System.Linq;

namespace Hst.Imager.Core.Helpers
{
    public static class FileAttributesFormatter
    {
        public static string FormatMsDosAttributes(int fileAttributes)
        {
            var fatAttributes = "ARHS";

            if (fileAttributes == 0)
            {
                return new string('-', fatAttributes.Length);
            }

            var orderedAttributes = new[]
            {
                (int)FileAttributes.Archive,
                (int)FileAttributes.ReadOnly,
                (int)FileAttributes.Hidden,
                (int)FileAttributes.System
            };

            return FormatAttributes(fatAttributes,
                orderedAttributes.Select(attribute => (fileAttributes & attribute) == attribute).ToArray());
        }

        private static string FormatAttributes(string attributes, bool[] presentAttributes)
        {
            var attributesArray = attributes.ToCharArray();
            for (var i = 0; i < presentAttributes.Length; i++)
            {
                if (presentAttributes[i])
                {
                    continue;
                }

                attributesArray[i] = '-';
            }

            return new string(attributesArray);
        }
    }
}
