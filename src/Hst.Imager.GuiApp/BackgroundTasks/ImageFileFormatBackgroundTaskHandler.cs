using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models.BackgroundTasks;
using Hst.Imager.Core;
using Hst.Imager.GuiApp.Extensions;
using Hst.Imager.GuiApp.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;
using Hst.Imager.Core.Models;
using Hst.Imager.GuiApp.Models;
using System;

namespace Hst.Imager.GuiApp.BackgroundTasks
{
    public class ImageFileFormatBackgroundTaskHandler : IBackgroundTaskHandler
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly IHubContext<ProgressHub> progressHubContext;
        private readonly AppState appState;

        public ImageFileFormatBackgroundTaskHandler(ILoggerFactory loggerFactory, IHubContext<ProgressHub> progressHubContext,
            AppState appState)
        {
            this.loggerFactory = loggerFactory;
            this.progressHubContext = progressHubContext;
            this.appState = appState;
        }
        public async ValueTask Handle(IBackgroundTaskContext context)
        {
            if (context.BackgroundTask is not ImageFileFormatBackgroundTask formatBackgroundTask)
            {
                return;
            }

            try
            {
                await progressHubContext.SendProgress(new Progress
                {
                    Title = formatBackgroundTask.Title,
                    IsComplete = false,
                    HasError = false,
                    ErrorMessage = null,
                    PercentComplete = 0
                }, context.Token);

                var commandHelper = new CommandHelper(this.loggerFactory.CreateLogger<ICommandHelper>(), appState.IsAdministrator);
                var logger = loggerFactory.CreateLogger<FormatCommand>();
                var formatCommand = new FormatCommand(logger, loggerFactory, commandHelper, Enumerable.Empty<IPhysicalDrive>(),
                    string.Concat(formatBackgroundTask.Path, formatBackgroundTask.Byteswap ? "+bs" : string.Empty),
                    formatBackgroundTask.FormatType, formatBackgroundTask.FileSystem, 
                    formatBackgroundTask.FileSystemPath,
                    appState.AppDataPath,
                    new Size(formatBackgroundTask.Size, Unit.Bytes));
                formatCommand.DataProcessed += async (_, args) =>
                {
                    await progressHubContext.SendProgress(new Progress
                    {
                        Title = formatBackgroundTask.Title,
                        IsComplete = false,
                        PercentComplete = args.PercentComplete,
                        BytesPerSecond = args.BytesPerSecond,
                        BytesProcessed = args.BytesProcessed,
                        BytesRemaining = args.BytesRemaining,
                        BytesTotal = args.BytesTotal,
                        MillisecondsElapsed = args.PercentComplete > 0
                            ? (long)args.TimeElapsed.TotalMilliseconds
                            : new long?(),
                        MillisecondsRemaining = args.PercentComplete > 0
                            ? (long)args.TimeRemaining.TotalMilliseconds
                            : new long?(),
                        MillisecondsTotal = args.PercentComplete > 0
                            ? (long)args.TimeTotal.TotalMilliseconds
                            : new long?()
                    }, context.Token);
                };
                var result = await formatCommand.Execute(context.Token);

                await Task.Delay(500, context.Token);

                await progressHubContext.SendProgress(new Progress
                {
                    Title = formatBackgroundTask.Title,
                    IsComplete = true,
                    HasError = result.IsFaulted,
                    ErrorMessage = result.IsFaulted ? result.Error.Message : null,
                    PercentComplete = 100
                }, context.Token);
            }
            catch (Exception e)
            {
                await progressHubContext.SendProgress(new Progress
                {
                    Title = formatBackgroundTask.Title,
                    IsComplete = true,
                    HasError = true,
                    ErrorMessage = e.Message,
                    PercentComplete = 100
                }, context.Token);
            }
        }
    }
}
