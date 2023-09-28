using Hst.Core;

namespace Hst.Imager.Core.Commands;

public class VersionNotFoundError : Error
{
    public VersionNotFoundError(string message) : base(message)
    {
    }
}