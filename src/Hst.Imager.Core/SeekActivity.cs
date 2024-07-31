using System;
using System.IO;

namespace Hst.Imager.Core;

public record SeekActivity(DateTime Date, long Position, long Offset, SeekOrigin Origin) : IStreamActivity
{
    public IStreamActivity.ActivityType Type => IStreamActivity.ActivityType.Seek;
}