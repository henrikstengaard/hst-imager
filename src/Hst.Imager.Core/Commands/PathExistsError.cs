using Hst.Core;

namespace Hst.Imager.Core.Commands;

public class PathExistsError(string message) : Error(message);