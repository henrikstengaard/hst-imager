﻿namespace Hst.Imager.GuiApp.Models.Requests
{
    using System.ComponentModel.DataAnnotations;

    public class ReadRequest
    {
        [Required]
        public string Title { get; set; }

        [Required]
        public string SourcePath { get; set; }

        [Required]
        public string DestinationPath { get; set; }
        
        public long Size { get; set; }
        public bool Verify { get; set; }
        public bool Force { get; set; }
        public int Retries { get; set; }

        public ReadRequest()
        {
            Verify = false;
            Force = false;
            Retries = 5;
        }
    }
}