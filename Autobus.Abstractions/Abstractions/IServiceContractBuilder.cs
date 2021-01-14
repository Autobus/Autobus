using System;
using Autobus.Abstractions;

namespace Autobus
{
    public interface IServiceContractBuilder
    {
        IServiceContractBuilder UseName(string name);

        IServiceContractBuilder AddInterface(Type interfaceType);

        IServiceContractBuilder AddInterface<T>() => AddInterface(typeof(T));

        IServiceContractBuilder AddRequest(Type requestType, Type responseType);

        IServiceContractBuilder AddRequest<TRequest, TResponse>() => AddRequest(typeof(TRequest), typeof(TResponse));

        IServiceContractBuilder AddCommand(Type commandType);

        IServiceContractBuilder AddCommand<TCommand>() => AddCommand(typeof(TCommand));

        IServiceContractBuilder AddEvent(Type eventType);

        IServiceContractBuilder AddEvent<TEvent>() => AddEvent(typeof(TEvent));

        IServiceContract Build();
    }
}
