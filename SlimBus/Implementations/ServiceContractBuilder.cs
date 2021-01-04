using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using SlimBus.Abstractions;
using SlimBus.Enums;
using SlimBus.Interfaces;
using SlimBus.Models;

namespace SlimBus.Implementations
{
    public class ServiceContractBuilder<TContract> : IServiceContractBuilder where TContract: BaseServiceContract, new()
    {
        private string? _name;

        private List<Type> _interfaceTypes = new();

        private HashSet<Type> _handledMessageTypes = new();
        
        private Dictionary<Type, Type> _requests = new();
        
        private List<MessageModel> _messages = new();

        public IServiceContractBuilder UseName(string name)
        {
            _name = name;
            return this;
        }
        
        public IServiceContractBuilder AddInterface(Type interfaceType)
        {
            _interfaceTypes.Add(interfaceType);
            return this;
        }
        
        public IServiceContractBuilder AddRequest(Type requestType, Type responseType)
        {
            if (!_handledMessageTypes.Add(requestType))
                throw new Exception($"Ambiguous use of {requestType.FullName} message.");
            if (!_handledMessageTypes.Add(responseType))
                throw new Exception($"Ambiguous use of {responseType.FullName} message.");
            var requestContract = new MessageModel(requestType, MessageBehavior.Request);
            var responseContract = new MessageModel(requestType, MessageBehavior.Response);
            _requests[requestContract.Type] = responseContract.Type;
            _messages.Add(requestContract);
            _messages.Add(responseContract);
            return this;
        }

        public IServiceContractBuilder AddEvent(Type eventType)
        {
            if (!_handledMessageTypes.Add(eventType))
                throw new Exception($"Ambiguous use of {eventType.FullName} message.");
            _messages.Add(new MessageModel(eventType, MessageBehavior.Event));
            return this;
        }

        public IServiceContractBuilder AddCommand(Type commandType)
        {
            if (!_handledMessageTypes.Add(commandType))
                throw new Exception($"Ambiguous use of {commandType.FullName} message.");
            _messages.Add(new MessageModel(commandType, MessageBehavior.Command));
            return this;
        }

        public IServiceContract Build()
        {
            var contract = new TContract();
            contract.Build(this);
            if (_name == null) 
                throw new ArgumentNullException($"{nameof(IServiceContractBuilder)} requires a name to be set.");
            var interfaceModels = new List<ServiceInterfaceModel>();
            foreach (var interfaceType in _interfaceTypes)
            {
                var interfaceModel = ServiceInterfaceModel.FromInterface(interfaceType);
                foreach (var command in interfaceModel.Commands)
                    AddCommand(command.CommandType);
                foreach (var requestResponse in interfaceModel.Requests)
                    AddRequest(requestResponse.RequestType, requestResponse.ResponseType);
            }
            contract.Name = _name;
            contract.Interfaces = interfaceModels;
            contract.Requests = new ReadOnlyDictionary<Type, Type>(_requests);
            contract.Messages = _messages;
            return contract;
        }
    }

    public class ServiceContractBuilder : ServiceContractBuilder<AnonymousServiceContract>
    {
        public static TContract Build<TContract>() where TContract : BaseServiceContract, new() 
            => (TContract) new ServiceContractBuilder<TContract>().Build();
    }
}
