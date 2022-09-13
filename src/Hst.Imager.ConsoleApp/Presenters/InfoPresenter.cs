namespace Hst.Imager.ConsoleApp.Presenters
{
    using System;
    using System.Collections.Generic;
    using HstWbInstaller.Imager.Core.Commands;
    using HstWbInstaller.Imager.Core.Extensions;

    public static class InfoPresenter
    {
        public static void PresentInfo(IEnumerable<MediaInfo> mediaInfos)
        {
            var diskNumber = 0;
            foreach (var mediaInfo in mediaInfos)
            {
                Console.WriteLine($"Disk {++diskNumber}:");
                PresentInfo(mediaInfo);
                Console.WriteLine();
            }
        }

        public static void PresentInfo(MediaInfo mediaInfo)
        {
            Console.WriteLine(
                $"Path: {mediaInfo.Path}");
            Console.WriteLine(
                $"Path: {mediaInfo.Model}");
            Console.WriteLine(
                $"Physical drive: {(mediaInfo.IsPhysicalDrive ? "Yes" : "No")}");
            Console.WriteLine(
                $"Type: {mediaInfo.Type}");
            Console.WriteLine(
                $"Disk size: {mediaInfo.DiskSize.FormatBytes()} ({mediaInfo.DiskSize} bytes)");
            Console.WriteLine("");

            RigidDiskBlockPresenter.Present(mediaInfo.RigidDiskBlock);
        }
    }
}