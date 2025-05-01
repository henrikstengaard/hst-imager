using System.Text.Json.Serialization;

namespace Hst.Imager.Core.Models;

public class ChildBlockDevice
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
    /// size of the device
    /// </summary>
    [JsonPropertyName("size")]
    public long? Size { get; set; }
}