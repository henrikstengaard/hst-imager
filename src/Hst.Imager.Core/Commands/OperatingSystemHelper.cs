namespace Hst.Imager.Core.Commands;

using System;
using Models;

public static class OperatingSystemHelper
{
    public static OperatingSystemEnum GetOperatingSystem()
    {
        if (OperatingSystem.IsWindows())
        {
            return OperatingSystemEnum.Windows;
        }
        if (OperatingSystem.IsMacOS())
        {
            return OperatingSystemEnum.MacOs;
        }
        return OperatingSystem.IsLinux() ? OperatingSystemEnum.Linux : OperatingSystemEnum.Other;
    }
}