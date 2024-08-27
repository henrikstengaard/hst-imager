namespace Hst.Imager.GuiApp.BackgroundTasks
{
    using System.Linq;
    using System.Threading.Tasks;
    using Extensions;
    using Hst.Imager.Core;
    using Hst.Imager.Core.Commands;
    using Hst.Imager.Core.Models.BackgroundTasks;
    using Hubs;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.Extensions.Logging;
    using Models;

    public class ImageFileInfoBackgroundTaskHandler : IBackgroundTaskHandler
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly IHubContext<ResultHub> resultHubContext;
        private readonly IHubContext<ErrorHub> errorHubContext;
        private readonly AppState appState;

        public ImageFileInfoBackgroundTaskHandler(ILoggerFactory loggerFactory, IHubContext<ResultHub> resultHubContext,
            IHubContext<ErrorHub> errorHubContext, AppState appState)
        {
            this.loggerFactory = loggerFactory;
            this.resultHubContext = resultHubContext;
            this.errorHubContext = errorHubContext;
            this.appState = appState;
        }

        public async ValueTask Handle(IBackgroundTaskContext context)
        {
            if (context.BackgroundTask is not ImageFileInfoBackgroundTask infoBackgroundTask)
            {
                return;
            }

            var commandHelper = new CommandHelper(this.loggerFactory.CreateLogger<ICommandHelper>(), appState.IsAdministrator);
            var logger = loggerFactory.CreateLogger<InfoCommand>();
            var infoCommand = new InfoCommand(logger, commandHelper, Enumerable.Empty<IPhysicalDrive>(),
                string.Concat(infoBackgroundTask.Path, infoBackgroundTask.Byteswap ? "+bs" : string.Empty));

            infoCommand.DiskInfoRead += async (_, args) =>
            {
                await resultHubContext.SendInfoResult(args.MediaInfo.ToViewModel());
            };

            var result = await infoCommand.Execute(context.Token);
            if (result.IsFaulted)
            {
                // send null info result for views to reset/clear
                await resultHubContext.SendInfoResult(null);
                
                var message = result.Error?.Message ?? "Info command returned error without message error";
                logger.LogError(message);
                await errorHubContext.SendError(message, context.Token);
            }
        }
    }
}