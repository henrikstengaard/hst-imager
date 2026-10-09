namespace Hst.Imager.Core.Models;

public enum FormatType
{
    Gpt,
    Mbr,
    Rdb,
    /// <summary>
    /// PiStorm formatted with Master Boot Record, same as PiStormMbr.
    /// </summary>
    PiStorm,
    /// <summary>
    /// PiStorm formatted with Master Boot Record.
    /// </summary>
    PiStormMbr,
    /// <summary>
    /// PiStorm formatted with Guid Partition Table.
    /// </summary>
    PiStormGpt
}