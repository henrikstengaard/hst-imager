using System.Collections.Generic;

namespace Hst.Imager.Core.Models
{
    using System.Text.Json.Serialization;

    public class BlockDevice
    {
        /// <summary>
        /// path to the device node
        /// </summary>
        [JsonPropertyName("path")]
        public string Path { get; set; }

        /// <summary>
        /// device type
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }

        /// <summary>
        /// device name
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// removable device
        /// </summary>
        [JsonPropertyName("rm")]
        public bool Removable { get; set; }

        /// <summary>
        /// device identifier
        /// </summary>
        [JsonPropertyName("model")]
        public string Model { get; set; }

        /// <summary>
        /// size of the device
        /// </summary>
        [JsonPropertyName("size")]
        public long? Size { get; set; }
        
        /// <summary>
        /// device vendor
        /// </summary>
        [JsonPropertyName("vendor")]
        public string Vendor { get; set; }
        
        /// <summary>
        /// device transport type
        /// </summary>
        [JsonPropertyName("tran")]
        public string Tran { get; set; }
        
        /// <summary>
        /// children
        /// </summary>
        [JsonPropertyName("children")]
        public IEnumerable<ChildBlockDevice> Children { get;set; }
    }
}