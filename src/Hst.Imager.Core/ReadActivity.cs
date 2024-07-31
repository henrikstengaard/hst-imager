using System;

namespace Hst.Imager.Core;

public record ReadActivity(DateTime Date, long Position, int Offset, int Count) : IStreamActivity
{
    public IStreamActivity.ActivityType Type => IStreamActivity.ActivityType.Read;
}