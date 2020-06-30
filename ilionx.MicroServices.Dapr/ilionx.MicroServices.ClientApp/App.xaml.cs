using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Refit;
using System;
using System.Configuration;
using System.Windows;

namespace ilionx.MicroServices.ClientApp
{

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly IServiceProvider _serviceProvider;

        private string ShapeListServiceBaseUrl { get; } = ConfigurationManager.AppSettings["shapeListServiceBaseUrl"];

        public App()
        {
            // use a dependency injection container
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            _serviceProvider = serviceCollection.BuildServiceProvider();
        }

        /// <summary>
        /// Configure the dependency injection container.
        /// </summary>
        /// <param name="services">Service collection to configure.</param>
        private void ConfigureServices(ServiceCollection services)
        {
            // main window instance
            services.AddSingleton<MainWindow>();

            // http API client for the ShapeList service
            services.AddRefitClient<IShapeListService>()
                .ConfigureHttpClient(config => config.BaseAddress = new Uri(ShapeListServiceBaseUrl));

            // SignalR connection for handling shape events
            services.AddSingleton<IShapeEventHandler>(provider =>
            {
                HubConnection connection = new HubConnectionBuilder()
                    .WithUrl($"{ShapeListServiceBaseUrl}/shapehub")
                    .WithAutomaticReconnect()
                    .Build();

                return new ShapeEventHandler(connection);
            });
        }

        /// <summary>
        /// Event called when starting the app.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // show the main window on start
            var mainWindow = _serviceProvider.GetService<MainWindow>();
            mainWindow.Show();
        }
    }
}
