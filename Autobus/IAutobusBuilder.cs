using Autobus.Abstractions;
using Autobus.Implementations;
using System;

namespace Autobus
{
    public interface IAutobusBuilder
    {
        IAutobusBuilder UseTransport<TTransportBuilder>(Action<TTransportBuilder> onBuild)
            where TTransportBuilder : ITransportBuilder, new();

        IAutobusBuilder UseService(IServiceContract serviceContract);

        IAutobusBuilder UseService<T>() where T : BaseServiceContract, new() =>
            UseService(ServiceContractBuilder.Build<T>());

        IAutobusBuilder UseServicesFromAllAssemblies();

        IAutobusBuilder UseSerializer(ISerializationProvider serializationProvider);

        IAutobusBuilder UseSerializer<T>() where T : ISerializationProvider, new() =>
            UseSerializer(new T());

        IAutobusBuilder UseCorrelationIdProvider(ICorrelationIdProvider correlationIdProvider);

        IAutobusBuilder UseCorrelationIdProvider<T>() where T : ICorrelationIdProvider, new() =>
            UseCorrelationIdProvider(new T());

        IAutobusBuilder UseRoutingDirectionProvider(IRoutingDirectionProvider routingDirectionProvider);

        IAutobusBuilder UseRoutingDirectionProvider<T>() where T : IRoutingDirectionProvider, new() =>
            UseRoutingDirectionProvider(new T());

        IAutobus Build();
    }
}
