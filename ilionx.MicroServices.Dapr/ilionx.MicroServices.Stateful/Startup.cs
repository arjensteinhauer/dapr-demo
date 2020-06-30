using ilionx.MicroServices.Stateful.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace ilionx.MicroServices.Stateful
{
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
            // add controller support with dapr support
            services
                .AddControllers()
                .AddDapr();

            // default json serializer settings
            services.AddSingleton(new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });

            // add SignalR support
            services.AddSignalR();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            // dapr: add middleware for handling cloud events (pub/sub)
            app.UseCloudEvents();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                // map the endpoint for dapr pub/sub subscriptions
                endpoints.MapSubscribeHandler();

                // map all controller enspoints
                endpoints.MapControllers();

                // map all SignalR hub endpoints
                endpoints.MapHub<ShapeHub>("/shapehub");
            });
        }
    }
}
