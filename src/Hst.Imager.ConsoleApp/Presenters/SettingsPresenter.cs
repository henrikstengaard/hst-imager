using System.Globalization;
using System.Text;
using Hst.Imager.Core.Models;

namespace Hst.Imager.ConsoleApp.Presenters;

public static class SettingsPresenter
{
    public static string PresentSettings(Settings settings)
    {
        var listTable = new Table
        {
            Columns =
            [
                new Column { Name = "Name" },
                new Column { Name = "Value" }
            ],
            Rows = 
            [
                new Row { Columns = ["All physical drives", settings.AllPhysicalDrives.ToString()] },
                new Row { Columns = ["Verify", settings.Verify.ToString()] },
                new Row { Columns = ["Force", settings.Force.ToString()] },
                new Row { Columns = ["Retries", settings.Retries.ToString(CultureInfo.InvariantCulture)] },
                new Row { Columns = ["Skip unused sectors", settings.SkipUnusedSectors.ToString()] },
                new Row { Columns = ["Use cache", settings.UseCache.ToString()] },
                new Row { Columns = ["Cache type", settings.CacheType.ToString()] }
            ]
        };

        var outputBuilder = new StringBuilder();
        outputBuilder.AppendLine("Settings:");
        outputBuilder.AppendLine();
        outputBuilder.Append(TablePresenter.Present(listTable));
        
        return outputBuilder.ToString();
    }
}