using SlimBus.Abstractions;
using SlimBus.Implementations;

namespace SlimBus
{
    public interface ISlimBusBuilder
    {
        ISlimBusBuilder UseTransport<TTransportFactory, TConfig>(TConfig config)
            where TTransportFactory : ITransportFactory<TConfig>, new()
            where TConfig : class;

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
