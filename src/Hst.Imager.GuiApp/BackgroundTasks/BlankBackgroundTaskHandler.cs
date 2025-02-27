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

    public class BlankBackgroundTaskHandler : IBackgroundTaskHandler
    {
        private readonly ILogger<BlankBackgroundTaskHandler> logger;
        private readonly ILoggerFactory loggerFactory;
        private readonly IHubContext<ProgressHub> progressHubContext;
        private readonly AppState appState;

        public BlankBackgroundTaskHandler(
            ILoggerFactory loggerFactory,
            IHubContext<ProgressHub> progressHubContext,
            AppState appState)
        {
            this.logger = loggerFactory.CreateLogger<BlankBackgroundTaskHandler>();
            this.loggerFactory = loggerFactory;
            this.progressHubContext = progressHubContext;
            this.appState = appState;
        }

        public async ValueTask Handle(IBackgroundTaskContext context)
        {
            if (context.BackgroundTask is not BlankBackgroundTask blankBackgroundTask)
            {
                return;
            }

            try
            {
                await progressHubContext.SendProgress(new Progress
                {
                    Title = blankBackgroundTask.Title,
                    IsComplete = false,
                    PercentComplete = 50,
                }, context.Token);

                var commandHelper = new CommandHelper(this.loggerFactory.CreateLogger<ICommandHelper>(), appState.IsAdministrator);
                var blankCommand = new BlankCommand(loggerFactory.CreateLogger<BlankCommand>(), commandHelper, blankBackgroundTask.Path,
                     new Size(blankBackgroundTask.Size, Unit.Bytes), blankBackgroundTask.CompatibleSize);

                var result = await blankCommand.Execute(context.Token);

                await Task.Delay(500, context.Token);
                
                await progressHubContext.SendProgress(new Progress
                {
                    Title = blankBackgroundTask.Title,
                    IsComplete = true,
                    HasError = result.IsFaulted,
                    ErrorMessage = result.IsFaulted ? result.Error.Message : null,
                    PercentComplete = 100
                }, context.Token);
            }
            catch (Exception e)
            {
                logger.LogError(e, "An unexpected error occured while executing blank command");

                await progressHubContext.SendProgress(new Progress
                {
                    Title = blankBackgroundTask.Title,
                    IsComplete = true,
                    HasError = true,
                    ErrorMessage = e.Message,
                    PercentComplete = 100
                }, context.Token);
            }
        }
    }
}