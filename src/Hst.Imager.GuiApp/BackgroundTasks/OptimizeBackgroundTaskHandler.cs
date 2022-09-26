namespace Hst.Imager.GuiApp.BackgroundTasks
{
    using System;
    using System.Threading.Tasks;
    using Core.Models;
    using Extensions;
    using Hst.Imager.Core.Commands;
    using Hst.Imager.Core.Models.BackgroundTasks;
    using Hubs;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.Extensions.Logging;
    using Models;

    public class OptimizeBackgroundTaskHandler : IBackgroundTaskHandler
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly IHubContext<ProgressHub> progressHubContext;
        private readonly AppState appState;

        public OptimizeBackgroundTaskHandler(
            ILoggerFactory loggerFactory,
            IHubContext<ProgressHub> progressHubContext, AppState appState)
        {
            this.loggerFactory = loggerFactory;
            this.progressHubContext = progressHubContext;
            this.appState = appState;
        }

        public async ValueTask Handle(IBackgroundTaskContext context)
        {
            if (context.BackgroundTask is not OptimizeBackgroundTask optimizeBackgroundTask)
            {
                return;
            }

            try
            {
                await progressHubContext.SendProgress(new Progress
                {
                    Title = optimizeBackgroundTask.Title,
                    IsComplete = false,
                    PercentComplete = 50,
                }, context.Token);

                var commandHelper = new CommandHelper(appState.IsAdministrator);
                var optimizeCommand = new OptimizeCommand(loggerFactory.CreateLogger<OptimizeCommand>(),commandHelper, optimizeBackgroundTask.Path, new Size(0, Unit.Bytes), false);

                var result = await optimizeCommand.Execute(context.Token);

                await Task.Delay(1000, context.Token);

                await progressHubContext.SendProgress(new Progress
                {
                    Title = optimizeBackgroundTask.Title,
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
                    Title = optimizeBackgroundTask.Title,
                    IsComplete = true,
                    HasError = true,
                    ErrorMessage = e.Message,
                    PercentComplete = 100
                }, context.Token);
            }
        }
    }
}