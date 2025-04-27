namespace Hst.Imager.GuiApp.BackgroundTasks
{
    using System;
    using System.Threading.Tasks;
    using Extensions;
    using Hst.Imager.Core.Commands;
    using Hst.Imager.Core.Models;
    using Hst.Imager.Core.Models.BackgroundTasks;
    using Hubs;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.Extensions.Logging;
    using Models;

    public class ConvertBackgroundTaskHandler : IBackgroundTaskHandler
    {
        private readonly ILogger<ConvertBackgroundTaskHandler> logger;
        private readonly ILoggerFactory loggerFactory;
        private readonly IHubContext<ProgressHub> progressHubContext;
        private readonly AppState appState;

        public ConvertBackgroundTaskHandler(
            ILoggerFactory loggerFactory,
            IHubContext<ProgressHub> progressHubContext,
            AppState appState)
        {
            this.logger = loggerFactory.CreateLogger<ConvertBackgroundTaskHandler>();
            this.loggerFactory = loggerFactory;
            this.progressHubContext = progressHubContext;
            this.appState = appState;
        }

        public async ValueTask Handle(IBackgroundTaskContext context)
        {
            if (context.BackgroundTask is not ConvertBackgroundTask convertBackgroundTask)
            {
                return;
            }

            try
            {
                using var commandHelper = new CommandHelper(this.loggerFactory.CreateLogger<ICommandHelper>(), appState.IsAdministrator);
                var convertCommand =
                    new ConvertCommand(loggerFactory.CreateLogger<ConvertCommand>(), commandHelper,
                        string.Concat(convertBackgroundTask.Byteswap ? "+bs:" : string.Empty, 
                            convertBackgroundTask.SourcePath),
                        convertBackgroundTask.DestinationPath, new Size(), false);
                convertCommand.DataProcessed += async (_, args) =>
                {
                    await progressHubContext.SendProgress(new Progress
                    {
                        Title = convertBackgroundTask.Title,
                        IsComplete = false,
                        PercentComplete = args.PercentComplete,
                        BytesPerSecond = args.BytesPerSecond,
                        BytesProcessed = args.BytesProcessed,
                        BytesRemaining = args.BytesRemaining,
                        BytesTotal = args.BytesTotal,
                        MillisecondsElapsed = args.PercentComplete > 0 ? (long)args.TimeElapsed.TotalMilliseconds : new long?(),
                        MillisecondsRemaining = args.PercentComplete > 0 ? (long)args.TimeRemaining.TotalMilliseconds : new long?(),
                        MillisecondsTotal = args.PercentComplete > 0 ? (long)args.TimeTotal.TotalMilliseconds : new long?()
                    }, context.Token);                
                };

                var result = await convertCommand.Execute(context.Token);
            
                await progressHubContext.SendProgress(new Progress
                {
                    Title = convertBackgroundTask.Title,
                    IsComplete = true,
                    HasError = result.IsFaulted,
                    ErrorMessage = result.IsFaulted ? result.Error.Message : null,
                    PercentComplete = 100
                }, context.Token);
            }
            catch (Exception e)
            {
                logger.LogError(e, "An unexpected error occured while executing convert command");

                await progressHubContext.SendProgress(new Progress
                {
                    Title = convertBackgroundTask.Title,
                    IsComplete = true,
                    HasError = true,
                    ErrorMessage = e.Message,
                    PercentComplete = 100
                }, context.Token);
            }
        }
    }
}