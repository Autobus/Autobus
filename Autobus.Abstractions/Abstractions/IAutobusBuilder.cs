using Autobus.Abstractions;
using System;
using System.Reflection;
using Autobus.Abstractions.Abstractions;

namespace Autobus
{
    public interface IAutobusBuilder
    {
        IAutobusBuilder UseTransport<TTransportBuilder>(Action<TTransportBuilder> onBuild)
            where TTransportBuilder : ITransportBuilder, new();

        IAutobusBuilder UseRequestTimeout(int milliseconds);
        
        IAutobusBuilder UseService(IServiceContract serviceContract);

        IAutobusBuilder UseService<T>() where T : BaseServiceContract, new();

        IAutobusBuilder UseServicesFromAssembly(Assembly assembly);

        IAutobusBuilder UseServicesFromAllAssemblies();

        IAutobusBuilder UseLogger(IAutobusLogger logger);

        IAutobusBuilder UseLogger<T>() where T : IAutobusLogger, new() => UseLogger(new T());

        IAutobusBuilder UseSerializer(ISerializationProvider serializationProvider);

        IAutobusBuilder UseSerializer<T>() where T : ISerializationProvider, new() => UseSerializer(new T());

        IAutobusBuilder UseCorrelationIdProvider(ICorrelationIdProvider correlationIdProvider);

        IAutobusBuilder UseCorrelationIdProvider<T>() where T : ICorrelationIdProvider, new() =>
            UseCorrelationIdProvider(new T());

        IAutobus Build();
    }
}
