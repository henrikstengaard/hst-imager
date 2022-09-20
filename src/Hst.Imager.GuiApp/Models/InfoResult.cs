namespace Hst.Imager.GuiApp.Models
{
    using System.Collections.Generic;
    using Hst.Imager.Core.Commands;

    public class InfoResult
    {
        public IEnumerable<MediaInfo> MediaInfos { get; set; }
    }
}