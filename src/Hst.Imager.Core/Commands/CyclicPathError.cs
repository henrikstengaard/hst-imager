using Hst.Core;

namespace Hst.Imager.Core.Commands;

public class CyclicPathError(string message) : Error(message);