using Autobus.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Autobus
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddServiceClient(this IServiceCollection services, Type serviceClientType) =>
            services.AddSingleton(serviceClientType, serviceProvider =>
            {
                var autobus = serviceProvider.GetRequiredService<IAutobus>();
                return autobus.GetServiceClient(serviceClientType);
            });

        public static IServiceCollection AddServiceClient<T>(this IServiceCollection services) => AddServiceClient(services, typeof(T));

        public static IServiceCollection AddServiceKernel<TService, TImplementation>(this IServiceCollection services, ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
            where TService : class
            where TImplementation : class, TService
        {
            if (serviceLifetime != ServiceLifetime.Scoped) throw new NotImplementedException();
            services.AddScoped<TService, TImplementation>();
            services.AddHostedService(serviceProvider =>
            {
                var autobus = serviceProvider.GetRequiredService<IAutobus>();
                var serviceContract = autobus.GetContractImplementingInterface<TService>();
                var hostedType = ServiceKernelTypeProvider.GenerateServiceKernelType(typeof(TService), serviceContract);
                var serviceInstance = (IHostedService)Activator.CreateInstance(hostedType, new object[] { serviceProvider, serviceContract, autobus });
                return new AutobusHostedServiceWrapper<TImplementation>(serviceInstance);
            });
            return services;
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
