using Hst.Core;

namespace Hst.Imager.Core.Commands;

public class SelfCopyError(string message) : Error(message);