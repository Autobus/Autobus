using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SlimBus.Abstractions;
using SlimBus.Implementations;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SlimBus.Extensions.Hosting
{
    public static class IHostBuilderExtensions
    {
        public static IHostBuilder UseSlimBus(this IHostBuilder hostBuilder, Action<ISlimBusBuilder> onBuild)
        {
            hostBuilder.ConfigureServices(services =>
            {
                var builder = new SlimBusBuilder();
                onBuild(builder);
                var slimBus = builder.Build();
                services.AddSingleton(slimBus);
                InjectServiceContracts(services, slimBus);
            });
            return hostBuilder;
        }

        public static IHostBuilder UseSlimBus(this IHostBuilder hostBuilder, Action<HostBuilderContext, ISlimBusBuilder> onBuild)
        {
            hostBuilder.ConfigureServices((hostBuilderContext, services) =>
            {
                var builder = new SlimBusBuilder();
                onBuild(hostBuilderContext, builder);
                var slimBus = builder.Build();
                services.AddSingleton(slimBus);
                InjectServiceContracts(services, slimBus);
            });
            return hostBuilder;
        }

        private static void InjectServiceContracts(IServiceCollection services, ISlimBus slimBus)
        {
            var serviceContracts = slimBus.GetServiceContracts();
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
                    var slimBus = serviceProvider.GetRequiredService<ISlimBus>();
                    return slimBus.GetServiceClient(serviceClientType);
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
                serviceCollection.AddHostedService<DynamicSlimBusHostedService<TImplementation>>(serviceProvider => 
                {
                    var slimBus = serviceProvider.GetRequiredService<ISlimBus>();
                    var serviceContract = slimBus.GetContractImplementingInterface<TService>();
                    var hostedType = ServiceKernelTypeProvider.GenerateServiceKernelType(typeof(TService), serviceContract);
                    var serviceInstance = (IHostedService)Activator.CreateInstance(hostedType, new object[] { serviceProvider, serviceContract, slimBus });
                    return new DynamicSlimBusHostedService<TImplementation>(serviceInstance);
                });
            });
            return hostBuilder;
        }

        private class DynamicSlimBusHostedService<TImplementation> : IHostedService
        {
            private readonly IHostedService _underlyingService;

            public DynamicSlimBusHostedService(IHostedService underlyingService)
            {
                _underlyingService = underlyingService;
            }

            public Task StartAsync(CancellationToken cancellationToken) => _underlyingService.StartAsync(cancellationToken);

            public Task StopAsync(CancellationToken cancellationToken) => _underlyingService.StopAsync(cancellationToken);
        }
    }
}
