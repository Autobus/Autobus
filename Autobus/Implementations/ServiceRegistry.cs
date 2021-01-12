using System;
using System.Collections.Generic;
using System.Linq;
using Autobus.Abstractions;

namespace Autobus.Implementations
{
    public sealed class ServiceRegistry : IServiceRegistry
    {
        private readonly IReadOnlyList<IServiceContract> _services;

        private readonly Dictionary<MessageModel, IServiceContract> _messageToService;

        private readonly Dictionary<Type, MessageModel> _typeToMessage;

        private readonly Dictionary<string, MessageModel> _nameToMessage;

        internal ServiceRegistry(IReadOnlyList<IServiceContract> services)
        {
            _services = services;
            _messageToService = new();
            _typeToMessage = new();
            _nameToMessage = new();
            foreach (var service in _services)
            {
                foreach (var message in service.Messages)
                {
                    if (!_nameToMessage.TryAdd(message.Name, message))
                        throw new Exception($"More than one message with name ${message.Name}");
                    _typeToMessage[message.Type] = message;
                    _messageToService[message] = service;
                }
            }
        }

        public MessageModel GetMessageModel(Type messageType)
        {
            if (!_typeToMessage.TryGetValue(messageType, out var message))
                throw new Exception($"Unknown message type: {messageType.Name}");
            return message;
        }

        public MessageModel GetMessageModel(string name)
        {
            if (!_nameToMessage.TryGetValue(name, out var message))
                throw new Exception($"Unknown message name: {name}");
            return message;
        }

        public IEnumerable<MessageModel> GetMessageModels() => _typeToMessage.Values;
        
        public MessageModel GetResponseModel(MessageModel request) => GetOwningService(request).Requests[request];

        public IReadOnlyList<IServiceContract> GetServiceContracts() => _services;

        public IServiceContract GetServiceContract(Type serviceContractType) =>
            _services.FirstOrDefault(contract => contract.GetType() == serviceContractType) ??
            throw new Exception($"No service contract with type {serviceContractType.Name}");

        public IServiceContract GetOwningService(MessageModel message) => _messageToService[message];

        public IServiceContract GetServiceImplementingInterface(Type interfaceType) =>
            _services.FirstOrDefault(contract => contract.Interfaces.Any(interfaceModel => interfaceModel.Interface == interfaceType)) ??
            throw new Exception($"No service implementing interface: {interfaceType.Name}");
    }
}
