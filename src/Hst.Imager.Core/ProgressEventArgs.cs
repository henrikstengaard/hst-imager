using System;
using Hst.Imager.Core.Models.BackgroundTasks;

namespace Hst.Imager.Core;

public class ProgressEventArgs(Progress progress) : EventArgs
{
    public readonly Progress Progress = progress;
}