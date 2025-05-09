namespace Hst.Imager.GuiApp
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using ElectronNET.API;
    using ElectronNET.API.Entities;
    using Helpers;
    using Core;
    using Hst.Imager.Core.Helpers;
    using Hst.Imager.Core.Models;
    using Hubs;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Hosting.Server.Features;
    using Microsoft.AspNetCore.SpaServices.ReactDevelopmentServer;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Middlewares;
    using Models;
    using Services;
    using OperatingSystem = Hst.Core.OperatingSystem;

    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var appDataPath = ApplicationDataHelper.GetApplicationDataDir(Core.Models.Constants.AppName);
            var hasDebugMode = ApplicationDataHelper.HasDebugEnabled(appDataPath, Core.Models.Constants.AppName)
                .GetAwaiter().GetResult();
#if BACKEND
            services.AddCors(options =>
            {
                options.AddPolicy("AllowLocalhost", builder =>
                {
                    builder.SetIsOriginAllowed(origin => new Uri(origin).Host == "localhost")
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();;
                });
            });
#endif
            services.AddSignalR(o =>
            {
                o.EnableDetailedErrors = hasDebugMode;
                o.MaximumReceiveMessageSize = 1024 * 1024;
            }).AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.Converters
                    .Add(new JsonStringEnumConverter());
            });

            services.AddControllersWithViews().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

            // In production, the React files will be served from this directory
            services.AddSpaStaticFiles(configuration =>
            {
                configuration.RootPath = "ClientApp/build";
            });

            services.AddHostedService<QueuedHostedService>();
            services.AddSingleton<IBackgroundTaskQueue>(new BackgroundTaskQueue(100));

            services.AddHostedService<BackgroundTaskService>();
            services.AddSingleton<IActiveBackgroundTaskList>(new ActiveBackgroundTaskList());
            services.AddSingleton(AppState.Create(
                appDataPath,
                string.Empty,
                OperatingSystem.IsAdministrator()));
            services.AddSingleton<PhysicalDriveManagerFactory>();
            services.AddSingleton<WorkerService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, AppState appState, ILogger<Startup> logger)
        {
            var addresses = app.ServerFeatures.Get<IServerAddressesFeature>().Addresses.ToList();
            logger.LogDebug($"Addresses = '{string.Join(",", addresses)}'");
            appState.BaseUrl = addresses.FirstOrDefault(x => x.StartsWith("https")) ?? addresses.FirstOrDefault();
            logger.LogDebug($"Base url = '{appState.BaseUrl}'");
            logger.LogDebug($"AppPath = '{appState.AppPath}'");

            // write base url to console, if debugger is attached.
            // used by vscode launch.json to open browser.
            if (Debugger.IsAttached)
            {
                Console.WriteLine($"Base url = '{appState.BaseUrl}'");
            }

#if BACKEND
            app.UseCors("AllowLocalhost");
#endif
            
            app.UseMiddleware<ExceptionMiddleware>();
            
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
#if (BACKEND == false)
            app.UseSpaStaticFiles();
#endif
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<ErrorHub>("/hubs/error");
                endpoints.MapHub<ProgressHub>("/hubs/progress");
                endpoints.MapHub<ShowDialogResultHub>("/hubs/show-dialog-result");
                endpoints.MapHub<WorkerHub>("/hubs/worker");
                endpoints.MapHub<ResultHub>("/hubs/result");
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller}/{action=Index}/{id?}");
            });

#if (BACKEND == false)
            app.UseSpa(spa =>
            {
                spa.Options.SourcePath = "ClientApp";

                if (env.IsDevelopment())
                {
                    spa.UseReactDevelopmentServer(npmScript: "start");
                }
            });

            Task.Run(() => ElectronBootstrap(appState.AppPath));
#endif
        }

        private async Task ElectronBootstrap(string appPath)
        {
            if (!HybridSupport.IsElectronActive)
            {
                return;
            }
            
            var browserWindow = await Electron.WindowManager.CreateWindowAsync(
                new BrowserWindowOptions
                {
                    Width = 1280,
                    Height = 720,
                    Center = true,
                    BackgroundColor = "#1A2933",
                    Frame = false,
                    WebPreferences = new WebPreferences
                    {
                        NodeIntegration = true,
                    },
                    Show = false,
                    Icon = Path.Combine(appPath, "ClientApp", "build", "icon.ico")
                });
            browserWindow.RemoveMenu();
            
            await browserWindow.WebContents.Session.ClearCacheAsync();

            browserWindow.OnClosed += () => Electron.App.Quit();
            browserWindow.OnReadyToShow += () => browserWindow.Show();
            browserWindow.OnMaximize += () => Electron.IpcMain.Send(browserWindow, "window-maximized");
            browserWindow.OnUnmaximize += () => Electron.IpcMain.Send(browserWindow, "window-unmaximized");

            var appDataPath = ApplicationDataHelper.GetApplicationDataDir(Core.Models.Constants.AppName);
            if (await ApplicationDataHelper.HasDebugEnabled(appDataPath, Core.Models.Constants.AppName))
            {
                browserWindow.WebContents.OpenDevTools();
            }
            
            await Electron.IpcMain.On("minimize-window", _ => browserWindow.Minimize());
            await Electron.IpcMain.On("maximize-window", _ => browserWindow.Maximize());
            await Electron.IpcMain.On("unmaximize-window", _ => browserWindow.Unmaximize());
            await Electron.IpcMain.On("close-window", _ => browserWindow.Close());
        }
    }
}
