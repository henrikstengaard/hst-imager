using Hst.Core;

namespace Hst.Imager.Core.Commands;

public class FileExistsError(string message) : Error(message);