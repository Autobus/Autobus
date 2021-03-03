using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

namespace Autobus
{
    public static class HostBuilderExtensions
    {
        public static IHostBuilder UseAutobus(this IHostBuilder hostBuilder, Action<IAutobusBuilder> onBuild) => 
            hostBuilder.ConfigureServices(services =>
            {
                services.AddSingleton(_ =>
                {
                    var builder = new AutobusBuilder();
                    onBuild(builder);
                    return builder.Build();
                });
            });

        public static IHostBuilder UseAutobus(this IHostBuilder hostBuilder, Action<HostBuilderContext, IAutobusBuilder> onBuild) => 
            hostBuilder.ConfigureServices((hostBuilderContext, services) =>
            {
                services.AddSingleton(_ =>
                {
                    var builder = new AutobusBuilder();
                    onBuild(hostBuilderContext, builder);
                    return builder.Build();
                });
            });
    }
}
