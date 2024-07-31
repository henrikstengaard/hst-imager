using System;

namespace Hst.Imager.Core;

public record FlushActivity(DateTime Date) : IStreamActivity
{
    public IStreamActivity.ActivityType Type => IStreamActivity.ActivityType.Flush;
}