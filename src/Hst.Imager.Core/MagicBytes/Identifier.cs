namespace Hst.Imager.Core.MagicBytes;

public record Identifier(int Offset, byte[] MagicBytes, DataType DataType);