using SlimBus.Abstractions;
using SlimBus.Implementations;
using System;

namespace SlimBus
{
    public interface ISlimBusBuilder
    {
        ISlimBusBuilder UseTransport<TTransportBuilder>(Action<TTransportBuilder> onBuild)
            where TTransportBuilder : ITransportBuilder, new();

        ISlimBusBuilder UseService(IServiceContract serviceContract);

        ISlimBusBuilder UseService<T>() where T : BaseServiceContract, new() =>
            UseService(ServiceContractBuilder.Build<T>());

        ISlimBusBuilder UseServicesFromAllAssemblies();

        ISlimBusBuilder UseSerializer(ISerializationProvider serializationProvider);

        ISlimBusBuilder UseSerializer<T>() where T : ISerializationProvider, new() =>
            UseSerializer(new T());

        ISlimBusBuilder UseCorrelationIdProvider(ICorrelationIdProvider correlationIdProvider);

        ISlimBusBuilder UseCorrelationIdProvider<T>() where T : ICorrelationIdProvider, new() =>
            UseCorrelationIdProvider(new T());

        ISlimBusBuilder UseRoutingDirectionProvider(IRoutingDirectionProvider routingDirectionProvider);

        ISlimBusBuilder UseRoutingDirectionProvider<T>() where T : IRoutingDirectionProvider, new() =>
            UseRoutingDirectionProvider(new T());

        ISlimBus Build();
    }
}
