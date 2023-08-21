using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetworkMonitor.Alert.Services;
using NetworkMonitor.Objects.Factory;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Threading;
using MetroLog;
using HostInitActions;


namespace NetworkMonitor.Service
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
            services.AddSingleton<IDataQueueService, DataQueueService>();
            services.AddSingleton<IAlertMessageService, AlertMessageService>();
            services.Configure<HostOptions>(s => s.ShutdownTimeout = TimeSpan.FromMinutes(5));
            services.AddSingleton<INetLoggerFactory, NetLoggerFactory>();
            services.AddSingleton(_cancellationTokenSource);


            var logger = LogManagerFactory.DefaultLogManager.GetLogger<Startup>();
            services.AddSingleton(logger);
            services.AddAsyncServiceInitialization()
               .AddInitAction<IAlertMessageService>(async (alertMessageService) =>
                    {
                        await alertMessageService.Init();
                    });
        }

      
    }
}
