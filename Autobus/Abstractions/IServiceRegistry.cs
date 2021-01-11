using Autobus.Enums;
using Autobus.Implementations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Autobus.Abstractions
{
    public interface IServiceRegistry
    {
        ServiceExchangeModel GetMessageExchange(Type messageType);

        ServiceExchangeModel GetMessageExchange<TMessage>() => GetMessageExchange(typeof(TMessage));

        MessageModel GetMessageModel(Type messageType);

        MessageModel GetMessageModel<TMessage>() => GetMessageModel(typeof(TMessage));

        IEnumerable<MessageModel> GetMessageModels();

        MessageModel GetResponseModel(Type requestType);

        MessageModel GetResponseModel<TResponse>() => GetResponseModel(typeof(TResponse));

        IReadOnlyList<IServiceContract> GetServiceContracts();

        IServiceContract GetServiceContract(Type serviceContractType);

        IServiceContract GetServiceContract<TServiceContract>() where TServiceContract : IServiceContract =>
            GetServiceContract(typeof(TServiceContract));

        IServiceContract? GetServiceImplementingInterface(Type interfaceType);

        IServiceContract? GetServiceImplementingInterface<T>() => GetServiceImplementingInterface(typeof(T));

        IEnumerable<ServiceExchangeModel> GetExchangeModels();
    }
}
