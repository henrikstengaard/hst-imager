﻿namespace Hst.Imager.Core.Models
{
    using System;
    using System.IO;

    public class Media : IDisposable
    {
        private bool disposed;
        
        public enum MediaType
        {
            Raw,
            Vhd
        }

        public string Path;
        public string Model;
        public long Size;
        public bool IsPhysicalDrive;
        public MediaType Type;

        public Stream Stream { get; private set; }

        public Media(string path, string name, long size, MediaType type, bool isPhysicalDrive, Stream stream)
        {
            Path = path;
            Model = name;
            Size = size;
            Type = type;
            IsPhysicalDrive = isPhysicalDrive; 
            Stream = stream;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                Stream?.Close();
                Stream?.Dispose();
                Stream = null;
            }

            disposed = true;
        }

        public void Dispose() => Dispose(true);
    }
}