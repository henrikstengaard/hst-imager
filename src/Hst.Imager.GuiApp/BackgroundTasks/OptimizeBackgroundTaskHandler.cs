namespace Hst.Imager.GuiApp.BackgroundTasks
{
    using System;
    using System.Threading.Tasks;
    using Core.Models;
    using Extensions;
    using Core.Commands;
    using Hst.Imager.Core.Models.BackgroundTasks;
    using Hubs;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.Extensions.Logging;
    using Models;

    public class OptimizeBackgroundTaskHandler : IBackgroundTaskHandler
    {
        private readonly ILogger<OptimizeBackgroundTaskHandler> logger;
        private readonly ILoggerFactory loggerFactory;
        private readonly IHubContext<ProgressHub> progressHubContext;
        private readonly AppState appState;

        public OptimizeBackgroundTaskHandler(
            ILoggerFactory loggerFactory,
            IHubContext<ProgressHub> progressHubContext,
            AppState appState)
        {
            this.logger = loggerFactory.CreateLogger<OptimizeBackgroundTaskHandler>();
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
                    PercentComplete = 50
                }, context.Token);

                using var commandHelper = new CommandHelper(this.loggerFactory.CreateLogger<ICommandHelper>(), appState.IsAdministrator);
                var optimizeCommand = new OptimizeCommand(
                    loggerFactory.CreateLogger<OptimizeCommand>(),
                    commandHelper, 
                    string.Concat(optimizeBackgroundTask.Byteswap ? "+bs:" : string.Empty, optimizeBackgroundTask.Path),
                    new Size(optimizeBackgroundTask.Size, Unit.Bytes), PartitionTable.None);

                var result = await optimizeCommand.Execute(context.Token);
                if (result.IsFaulted)
                {
                    await progressHubContext.SendProgress(new Progress
                    {
                        Title = optimizeBackgroundTask.Title,
                        IsComplete = true,
                        HasError = true,
                        ErrorMessage = result.Error.ToString(),
                        PercentComplete = 100
                    }, context.Token);
                    return;
                }

                await Task.Delay(500, context.Token);

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
                logger.LogError(e, "An unexpected error occured while executing optimize command");

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