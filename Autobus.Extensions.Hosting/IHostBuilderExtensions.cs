using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Autobus.Implementations;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Autobus.Extensions.Hosting
{
    public static class IHostBuilderExtensions
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

        public static IHostBuilder UseServiceClient(this IHostBuilder hostBuilder, Type serviceClientType) =>
            hostBuilder.ConfigureServices(services =>
            {
                services.AddSingleton(serviceClientType, serviceProvider => 
                {
                    var autobus = serviceProvider.GetRequiredService<IAutobus>();
                    return autobus.GetServiceClient(serviceClientType);
                });
            });

        public static IHostBuilder AddServiceClient<T>(this IHostBuilder hostBuilder) => UseServiceClient(hostBuilder, typeof(T));

        public static IHostBuilder AddServiceKernel<TService, TImplementation>(this IHostBuilder hostBuilder, ServiceLifetime serviceLifetime=ServiceLifetime.Scoped) 
            where TService : class
            where TImplementation : class, TService
        {
            if (serviceLifetime != ServiceLifetime.Scoped) throw new NotImplementedException();
            hostBuilder.ConfigureServices(serviceCollection =>
            {
                serviceCollection.AddScoped<TService, TImplementation>();
                serviceCollection.AddHostedService(serviceProvider => 
                {
                    var autobus = serviceProvider.GetRequiredService<IAutobus>();
                    var serviceContract = autobus.GetContractImplementingInterface<TService>();
                    var hostedType = ServiceKernelTypeProvider.GenerateServiceKernelType(typeof(TService), serviceContract);
                    var serviceInstance = (IHostedService)Activator.CreateInstance(hostedType, new object[] { serviceProvider, serviceContract, autobus });
                    return new AutobusHostedServiceWrapper<TImplementation>(serviceInstance);
                });
            });
            return hostBuilder;
        }

        private class AutobusHostedServiceWrapper<TImplementation> : IHostedService
        {
            private readonly IHostedService _underlyingService;

            public AutobusHostedServiceWrapper(IHostedService underlyingService)
            {
                _underlyingService = underlyingService;
            }

            public Task StartAsync(CancellationToken cancellationToken) => _underlyingService.StartAsync(cancellationToken);

            public Task StopAsync(CancellationToken cancellationToken) => _underlyingService.StopAsync(cancellationToken);
        }
    }
}
