using Hst.Core;

namespace Hst.Imager.Core.Volumes;

public class VolumeReadOnlyError(string message) : Error(message);
