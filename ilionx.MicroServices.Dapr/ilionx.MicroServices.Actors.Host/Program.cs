using Dapr.Actors.AspNetCore;
using ilionx.MicroServices.Actors.Service;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;

namespace ilionx.MicroServices.Actors.Host
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateWebHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .UseActors(actorRuntime =>
                {
                    // register the shape actor
                    actorRuntime.RegisterActor<ShapeActor>();
                });
    }
}
