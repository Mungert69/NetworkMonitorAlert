using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetworkMonitor.Alert.Services;
using NetworkMonitor.Objects.Factory;
using NetworkMonitor.Objects.Repository;
using NetworkMonitor.Objects;
using NetworkMonitor.Utils.Helpers;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using HostInitActions;


namespace NetworkMonitor.Alert
{
    public class Startup
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        public Startup(IConfiguration configuration)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        private IServiceCollection _services;

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            _services = services;
            services.AddLogging(builder =>
               {
                   builder.AddSimpleConsole(options =>
                        {
                            options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
                            options.IncludeScopes = true;
                        });
               });

            services.AddSingleton<IDataQueueService, DataQueueService>();
            services.AddSingleton<IAlertMessageService, AlertMessageService>();
            services.Configure<HostOptions>(s => s.ShutdownTimeout = TimeSpan.FromSeconds(30));
            services.AddSingleton(_cancellationTokenSource);
            services.AddSingleton<IRabbitRepo, RabbitRepo>();
            services.AddSingleton<IRabbitListener, RabbitListener>();
            services.AddSingleton<ISystemParamsHelper, SystemParamsHelper>();
            services.AddSingleton<AlertParams>(sp =>
         {
             var systemParamsHelper = sp.GetRequiredService<ISystemParamsHelper>();
             return systemParamsHelper.GetAlertParams();
         });
            services.AddSingleton<SystemParams>(sp =>
           {
               var systemParamsHelper = sp.GetRequiredService<ISystemParamsHelper>();
               return systemParamsHelper.GetSystemParams();
           });
            services.AddSingleton<IProcessorStateRabbitListner, ProcessorStateRabbitListner>();
            services.AddSingleton<IProcessorState, ProcessorState>();


            services.AddSingleton<IFileRepo, FileRepo>(
                 provider =>
                 {
                     return new FileRepo(false, "./state/networkmonitoralert");
                 }
             );
            services.AddAsyncServiceInitialization()
                .AddInitAction<IRabbitRepo>(async (rabbitRepo) =>
                    {
                        await rabbitRepo.ConnectAndSetUp(_cancellationTokenSource.Token);
                    })
                .AddInitAction<IAlertMessageService>(async (alertMessageService) =>
                    {
                        await alertMessageService.Init();
                    })
                .AddInitAction<IRabbitListener>(async (rabbitListener) =>
                    {
                        await rabbitListener.Setup(_cancellationTokenSource.Token);
                    })
                .AddInitAction<IProcessorStateRabbitListner>(async (processorStateRabbitListener) =>
                    {
                        await processorStateRabbitListener.Setup(_cancellationTokenSource.Token);
                    });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime appLifetime)
        {
            appLifetime.ApplicationStopping.Register(() =>
            {
                _cancellationTokenSource.Cancel();

                var rabbitRepo = app.ApplicationServices.GetService<IRabbitRepo>();
                if (rabbitRepo != null)
                {
                    rabbitRepo.Shutdown().GetAwaiter().GetResult();
                }

                var rabbitListener = app.ApplicationServices.GetService<IRabbitListener>();
                if (rabbitListener != null)
                {
                    rabbitListener.Shutdown().GetAwaiter().GetResult();
                }

                var processorStateRabbitListener = app.ApplicationServices.GetService<IProcessorStateRabbitListner>();
                if (processorStateRabbitListener != null)
                {
                    processorStateRabbitListener.Shutdown().GetAwaiter().GetResult();
                }
            });
        }

    }
}
