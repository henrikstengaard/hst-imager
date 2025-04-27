using System;
using Hst.Imager.Core.Commands;

namespace Hst.Imager.Core;

public class MediaInfoEventArgs(MediaInfo mediaInfo) : EventArgs
{
    public readonly MediaInfo MediaInfo = mediaInfo;
}