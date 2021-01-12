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
        MessageModel GetMessageModel(Type messageType);

        MessageModel GetMessageModel<TMessage>() => GetMessageModel(typeof(TMessage));

        MessageModel GetMessageModel(string name);
        
        IEnumerable<MessageModel> GetMessageModels();

        MessageModel GetResponseModel(MessageModel request);

        MessageModel GetResponseModel(Type requestType) => GetResponseModel(GetMessageModel(requestType));

        MessageModel GetResponseModel<TRequest>() => GetResponseModel(typeof(TRequest));
        
        IReadOnlyList<IServiceContract> GetServiceContracts();

        IServiceContract GetServiceContract(Type serviceContractType);

        IServiceContract GetServiceContract<TServiceContract>() where TServiceContract : IServiceContract =>
            GetServiceContract(typeof(TServiceContract));

        IServiceContract GetOwningService(MessageModel message);

        IServiceContract GetOwningService(Type messageType) => GetOwningService(GetMessageModel(messageType));
        
        IServiceContract GetOwningService<TMessage>() => GetOwningService(typeof(TMessage));
        
        IServiceContract? GetServiceImplementingInterface(Type interfaceType);

        IServiceContract? GetServiceImplementingInterface<T>() => GetServiceImplementingInterface(typeof(T));
    }
}
