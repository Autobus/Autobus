using System;
using System.Threading.Tasks;
using SlimBus.Implementations;
using SlimBus.Abstractions;
using SlimBus.Delegates;
using System.Collections.Generic;

namespace SlimBus
{
    public interface ISlimBus
    {
        void Subscribe<TMessage>(OnMessageDelegate<TMessage> onMessage);

        void Subscribe<TRequest, TResponse>(OnRequestDelegate<TRequest, TResponse> onRequest);

        void Unsubscribe<TRequest, TResponse>(OnRequestDelegate<TRequest, TResponse> onRequest);

        void Unsubscribe<TMessage>(OnMessageDelegate<TMessage> onMessage);

        void Bind(object obj, IServiceContract serviceContract);

        void Bind<TServiceContract>(object obj) where TServiceContract : BaseServiceContract, new() =>
            Bind(obj, ServiceContractBuilder.Build<TServiceContract>());

        void Unbind(object obj);

        void Publish<TMessage>(TMessage message);

        Task<TResponse> Publish<TRequest, TResponse>(TRequest request);

        object GetServiceClient(Type interfaceType);

        TClientInterface GetServiceClient<TClientInterface>()
            => (TClientInterface) GetServiceClient(typeof(TClientInterface));

        IServiceContract GetServiceContract(Type serviceContractType);

        IServiceContract GetServiceContract<TServiceContract>() where TServiceContract : IServiceContract =>
            GetServiceContract(typeof(TServiceContract));

        IReadOnlyList<IServiceContract> GetServiceContracts();

        IServiceContract? GetContractImplementingInterface(Type interfaceType);

        IServiceContract? GetContractImplementingInterface<T>() => GetContractImplementingInterface(typeof(T));
    }
}
