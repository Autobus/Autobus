using System;
using Autobus.Abstractions;

namespace Autobus
{
    public interface IServiceContractBuilder
    {
        IServiceContractBuilder UseName(string Name);

        IServiceContractBuilder AddInterface(Type interfaceType);

        IServiceContractBuilder AddInterface<T>() => AddInterface(typeof(T));

        IServiceContractBuilder AddRequest(Type requestType, Type responseType);

        IServiceContractBuilder AddRequest<TRequest, TResponse>() => AddRequest(typeof(TRequest), typeof(TResponse));

        IServiceContractBuilder AddCommand(Type commandType);

        IServiceContractBuilder AddCommand<T>() => AddCommand(typeof(T));

        IServiceContractBuilder AddEvent(Type eventType);

        IServiceContractBuilder AddEvent<T>() => AddEvent(typeof(T));

        IServiceContract Build();
    }
}
