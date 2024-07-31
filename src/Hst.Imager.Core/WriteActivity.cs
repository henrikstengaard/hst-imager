using System;

namespace Hst.Imager.Core;

public record WriteActivity(DateTime Date, long Position, int Offset, int Count) : IStreamActivity
{
    public IStreamActivity.ActivityType Type => IStreamActivity.ActivityType.Write;
}