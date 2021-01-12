using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Autobus.Implementations;
using System;

namespace Autobus
{
    public static class HostBuilderExtensions
    {
        public static IHostBuilder UseAutobus(this IHostBuilder hostBuilder, Action<IAutobusBuilder> onBuild) => 
            hostBuilder.ConfigureServices(services =>
            {
                var builder = new AutobusBuilder();
                onBuild(builder);
                var autobus = builder.Build();
                services.AddSingleton(autobus);
                InjectServiceContracts(services, autobus);
            });

        public static IHostBuilder UseAutobus(this IHostBuilder hostBuilder, Action<HostBuilderContext, IAutobusBuilder> onBuild) => 
            hostBuilder.ConfigureServices((hostBuilderContext, services) =>
            {
                var builder = new AutobusBuilder();
                onBuild(hostBuilderContext, builder);
                var autobus = builder.Build();
                services.AddSingleton(autobus);
                InjectServiceContracts(services, autobus);
            });

        private static void InjectServiceContracts(IServiceCollection services, IAutobus autobus)
        {
            var serviceContracts = autobus.GetServiceContracts();
            foreach (var serviceContract in serviceContracts)
            {
                if (serviceContract is AnonymousServiceContract)
                    continue;
                services.AddSingleton(serviceContract.GetType(), serviceContract);
            }
        }
    }
}
