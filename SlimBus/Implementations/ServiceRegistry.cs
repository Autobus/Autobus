using System;
using System.Collections.Generic;
using System.Linq;
using SlimBus.Enums;
using SlimBus.Abstractions;

namespace SlimBus.Implementations
{
    public sealed class ServiceRegistry : IServiceRegistry
    {
        private readonly IReadOnlyList<IServiceContract> _serviceContracts;

        private readonly Dictionary<Type, ServiceExchangeModel> _messageToExchange;

        private readonly Dictionary<Type, MessageModel> _messageModels;

        private readonly List<ServiceExchangeModel> _exchangeModels;

        internal ServiceRegistry(IReadOnlyList<IServiceContract> serviceContracts)
        {
            _serviceContracts = serviceContracts;
            _exchangeModels = GenerateExchangeModels(_serviceContracts).ToList();
            _messageModels = GenerateMessageModelMap(_serviceContracts);
            _messageToExchange = GenerateExchangeMessageMap(_exchangeModels);
        }

        public IEnumerable<ServiceExchangeModel> GetExchangeModels() => _exchangeModels;

        public ServiceExchangeModel GetMessageExchange(Type messageType) => _messageToExchange[messageType];

        public MessageModel GetMessageModel(Type messageType) => _messageModels[messageType];

        public IEnumerable<MessageModel> GetMessageModels() => _messageModels.Values;

        public MessageModel GetResponseModel(Type requestType)
        {
            var exchange = GetMessageExchange(requestType);
            var responseType = exchange.ServiceContract.Requests[requestType];
            return GetMessageModel(responseType);
        }

        public IReadOnlyList<IServiceContract> GetServiceContracts() => _serviceContracts;

        public IServiceContract? GetServiceContract(Type serviceContractType) =>
            _serviceContracts.FirstOrDefault(contract => contract.GetType() == serviceContractType);

        public IServiceContract? GetServiceImplementingInterface(Type interfaceType) =>
            _serviceContracts.FirstOrDefault(contract => contract.Interfaces.Any(interfaceModel => interfaceModel.Interface == interfaceType));

        private static IEnumerable<ServiceExchangeModel> GenerateExchangeModels(IEnumerable<IServiceContract> serviceContracts)
        {
            foreach (var contract in serviceContracts)
            {
                yield return new (contract, ExchangeType.Topic);
                if (contract.Messages.Any(message => message.Behavior == MessageBehavior.Event))
                    yield return new (contract, ExchangeType.Fanout);
            }
        }

        private static Dictionary<Type, ServiceExchangeModel> GenerateExchangeMessageMap(IEnumerable<ServiceExchangeModel> exchangeModels)
        {
            var map = new Dictionary<Type, ServiceExchangeModel>();
            foreach (var exchangeModel in exchangeModels)
            {
                var messages = exchangeModel.ExchangeType switch
                {
                    ExchangeType.Topic => exchangeModel.ServiceContract.Messages
                        .Where(model => model.Behavior == MessageBehavior.Command || 
                                        model.Behavior == MessageBehavior.Request ||
                                        model.Behavior == MessageBehavior.Response)
                        .Select(model => model.Type),
                    ExchangeType.Fanout => exchangeModel.ServiceContract.Messages
                        .Where(model => model.Behavior == MessageBehavior.Event)
                        .Select(model => model.Type),
                    _ => throw new NotImplementedException()
                };
                foreach (var message in messages) map[message] = exchangeModel;
            }
            return map;
        }

        private static Dictionary<Type, MessageModel> GenerateMessageModelMap(IEnumerable<IServiceContract> serviceContracts)
        {
            var map = new Dictionary<Type, MessageModel>();
            foreach (var contract in serviceContracts)
                foreach (var message in contract.Messages)
                    if (!map.TryAdd(message.Type, message)) 
                        throw new Exception($"Ambigous use of message {message.Type.Name}");
            return map;
        }
    }
}
