using System;
using System.Threading.Tasks;
using System.Linq.Expressions;
using Autobus.Abstractions;
using Autobus.Delegates;
using Autobus.Enums;
using System.Collections.Generic;
using System.Reflection;
using Autobus.Providers;
using Autobus.Models;

namespace Autobus
{
    public class Autobus : IAutobus
    {
        private readonly IServiceRegistry _serviceRegistry;

        private readonly ISerializationProvider _serializationProvider;

        private readonly ICorrelationIdProvider _correlationIdProvider;

        private readonly ITransport _transport;

        private readonly Dictionary<Type, object> _serviceClients = new();

        private readonly Dictionary<Type, Delegate> _messageSubscriptions = new();

        private readonly Dictionary<Type, List<Delegate>> _eventSubscriptions = new();

        private readonly Dictionary<Type, nint> _messageHandlerFuncs = new();

        internal Autobus(IServiceRegistry serviceRegistry, ISerializationProvider serializationProvider, ICorrelationIdProvider correlationIdProvider, ITransport transport)
        {
            _serviceRegistry = serviceRegistry;
            _serializationProvider = serializationProvider;
            _correlationIdProvider = correlationIdProvider;
            _transport = transport;
        }

        public void Publish<TMessage>(TMessage message)
        {
            var messageModel = _serviceRegistry.GetMessageModel<TMessage>();
            var service = _serviceRegistry.GetOwningService(messageModel);
            _serializationProvider.Serialize(message, (Transport: _transport, Service: service, Message: messageModel), 
                (data, state) =>
            {
                state.Transport.Publish(state.Service, state.Message, data);
            });
        }

        public Task<TResponse> Publish<TRequest, TResponse>(TRequest message)
        {
            var messageModel = _serviceRegistry.GetMessageModel<TRequest>();
            var service = _serviceRegistry.GetOwningService(messageModel);
            var requestId = _correlationIdProvider.GetNextCorrelationId();
            var request = _transport.CreateNewRequest(requestId);
            _serializationProvider.Serialize(message, (Transport: _transport, Service: service, Message: messageModel, Request: request), (data, state) =>
            {
                state.Transport.Publish(state.Service, state.Message, data, state.Request);
            });
            return WaitForResponse<TResponse>(request);
        }

        private async Task<TResponse> WaitForResponse<TResponse>(ServiceRequestModel request)
        {
            // TODO: Handle timeouts here. If we wait too long we call DiscardRequest on the transport
            var response = await request.ResponseTask.Task;
            try
            {
                var deserialized = _serializationProvider.Deserialize<TResponse>(response.Data);
                _transport.Acknowledge(response.Sender);
                return deserialized;
            }
            catch
            {
                // TODO: We probably want to do some more error stuff here
                _transport.Reject(response.Sender);
                throw;
            }
        }

        internal unsafe Task HandleMessage(string identity, ReadOnlyMemory<byte> data, object sender)
        {
            var messageModel = _serviceRegistry.GetMessageModel(identity);
            var messageType = messageModel.Type;
            if (messageModel.Behavior == MessageBehavior.Request)
            {
                if (!_messageSubscriptions.TryGetValue(messageType, out var messageHandler))
                {
                    // TODO: Log warning
                    throw new Exception();
                }
                var handlerPtr = (delegate*<Autobus, Delegate, ReadOnlyMemory<byte>, object, Task>)_messageHandlerFuncs[messageType];
                return handlerPtr(this, messageHandler, data, sender);
            }
            else if (messageModel.Behavior == MessageBehavior.Command)
            {
                if (!_messageSubscriptions.TryGetValue(messageType, out var messageHandler))
                {
                    // TODO: Log warning
                    throw new Exception();
                }
                var handlerPtr = (delegate*<Autobus, Delegate, ReadOnlyMemory<byte>, Task>)_messageHandlerFuncs[messageType];
                return handlerPtr(this, messageHandler, data);
            }
            else if (messageModel.Behavior == MessageBehavior.Event)
            {
                if (!_eventSubscriptions.TryGetValue(messageType, out var subscribers))
                {
                    // TODO: Log warning
                    throw new Exception();
                }
                var handlerPtr = (delegate*<Autobus, Delegate, ReadOnlyMemory<byte>, Task>)_messageHandlerFuncs[messageType];
                if (subscribers.Count == 1)
                    return handlerPtr(this, subscribers[0], data);
                var eventTasks = new Task[subscribers.Count];
                for (var i = 0; i < eventTasks.Length; i++)
                    eventTasks[i] = handlerPtr(this, subscribers[i], data);
                return Task.WhenAll(eventTasks);
            }
            else
                throw new Exception();
        }

        public unsafe void Subscribe<TRequest, TResponse>(OnRequestDelegate<TRequest, TResponse> onRequest)
        {
            var requestModel = _serviceRegistry.GetMessageModel<TRequest>();
            var responseModel = _serviceRegistry.GetMessageModel<TResponse>();
            var owningService = _serviceRegistry.GetOwningService(requestModel);
            if (owningService.Requests.TryGetValue(requestModel, out var boundResponse) && 
                boundResponse != responseModel)
                throw new Exception($"Invalid request response pair: {requestModel.Name}/{responseModel.Name}");
            if (!_messageSubscriptions.TryAdd(requestModel.Type, onRequest))
                throw new Exception($"Already bound to request type: {requestModel.Name}");
            delegate*<Autobus, Delegate, ReadOnlyMemory<byte>, object, Task> handlerPtr = &HandleIncomingRequest<TRequest, TResponse>;
            _messageHandlerFuncs[requestModel.Type] = (nint)handlerPtr;
            BindTo<TRequest>();
        }

        public void Unsubscribe<TRequest, TResponse>(OnRequestDelegate<TRequest, TResponse> onRequest) => Unsubscribe(typeof(TRequest), onRequest);

        public unsafe void Subscribe<TMessage>(OnMessageDelegate<TMessage> onMessage)
        {
            var message = _serviceRegistry.GetMessageModel<TMessage>();
            switch (message.Behavior)
            {
                case MessageBehavior.Command:
                {
                    if (!_messageSubscriptions.TryAdd(message.Type, onMessage))
                        throw new Exception($"Already bound to command type: {message.Name}");
                    break;
                }
                case MessageBehavior.Event:
                {
                    if (!_eventSubscriptions.TryGetValue(message.Type, out var subscriptions))
                        subscriptions = _eventSubscriptions[message.Type] = new();
                    if (subscriptions.Contains(onMessage))
                        throw new Exception();
                    subscriptions.Add(onMessage);
                    break;
                }
                default:
                    throw new Exception($"Cant bind to message: {message}");
            }
            delegate*<Autobus, Delegate, ReadOnlyMemory<byte>, Task> handlerPtr = &HandleIncomingMessage<TMessage>;
            _messageHandlerFuncs[message.Type] = (nint)handlerPtr;
            BindTo<TMessage>();
        }

        public void Unsubscribe<TCommand>(OnMessageDelegate<TCommand> onCommand) => Unsubscribe(typeof(TCommand), onCommand);

        private void Unsubscribe(Type messageType, Delegate onMessage)
        {
            var messageModel = _serviceRegistry.GetMessageModel(messageType);
            if (messageModel.Behavior == MessageBehavior.Event)
            {
                if (!_eventSubscriptions.TryGetValue(messageType, out var subscriptions))
                    throw new Exception();
                subscriptions.Remove(onMessage);
                if (subscriptions.Count == 0)
                {
                    _eventSubscriptions.Remove(messageType);
                    UnbindFrom(messageType);
                }
            }
            else
            {
                if (!_messageSubscriptions.Remove(messageType))
                    throw new Exception();
                UnbindFrom(messageType);
            }
        }

        public void Bind(object obj, IServiceContract serviceContract)
        {
            if (serviceContract.Interfaces.Count == 0)
                throw new Exception($"Unbindable service contract: {serviceContract.Name}");
            // Verify we implement all the interfaces
            var objType = obj.GetType();
            var objInterfaces = objType.GetInterfaces();
            foreach (var i in serviceContract.Interfaces)
            {
                if (!Array.Exists(objInterfaces, element => element == i.Interface))
                    throw new Exception("Needs to implement something!");
            }
            foreach (var interfaceModel in serviceContract.Interfaces)
            {
                foreach (var requestModel in interfaceModel.Requests)
                {
                    var bindRequestMethod = typeof(Autobus).GetMethod("BindObjectRequestHandler").MakeGenericMethod(new[] { requestModel.RequestType, requestModel.ResponseType });
                    bindRequestMethod.Invoke(this, new object[] { obj, requestModel.RequestHandler });
                }
                foreach (var commandModel in interfaceModel.Commands)
                {
                    var bindCommandMethod = typeof(Autobus).GetMethod("BindObjectCommandHandler").MakeGenericMethod(new[] { commandModel.CommandType });
                    bindCommandMethod.Invoke(this, new[] { obj, commandModel.CommandHandler });
                }
            }
        }

        public void BindObjectRequestHandler<TRequest, TResponse>(object obj, MethodInfo method)
        {
            var requestParameter = Expression.Parameter(typeof(TRequest));
            var onRequest = (OnRequestDelegate<TRequest, TResponse>)Expression.Lambda(
                typeof(OnRequestDelegate<TRequest, TResponse>), 
                Expression.Call(Expression.Constant(obj), method, requestParameter), 
                requestParameter)
                .Compile();
            Subscribe(onRequest);
        }

        public void BindObjectCommandHandler<TCommand>(object obj, MethodInfo method)
        {
            var commandParameter = Expression.Parameter(typeof(TCommand));
            var onCommand = (OnMessageDelegate<TCommand>)Expression.Lambda(
                typeof(OnMessageDelegate<TCommand>), 
                Expression.Call(Expression.Constant(obj), method, commandParameter), 
                commandParameter)
                .Compile();
            Subscribe(onCommand);
        }

        public void Unbind(object obj)
        {
        }

        private void BindTo(Type messageType)
        {
            var message = _serviceRegistry.GetMessageModel(messageType);
            var service = _serviceRegistry.GetOwningService(message);
            _transport.BindTo(service, message);
        }

        private void BindTo<TMessage>() => BindTo(typeof(TMessage));

        private void UnbindFrom(Type messageType)
        {
            var message = _serviceRegistry.GetMessageModel(messageType);
            var service = _serviceRegistry.GetOwningService(message); 
            _transport.UnbindFrom(service, message);
        }

        private void UnbindFrom<TMessage>() => UnbindFrom(typeof(TMessage));

        public IServiceContract GetServiceContract(Type serviceContractType) => _serviceRegistry.GetServiceContract(serviceContractType);

        public IReadOnlyList<IServiceContract> GetServiceContracts() => _serviceRegistry.GetServiceContracts();

        public IServiceContract? GetContractImplementingInterface(Type interfaceType) => _serviceRegistry.GetServiceImplementingInterface(interfaceType);

        public object GetServiceClient(Type interfaceType)
        {
            if (_serviceClients.TryGetValue(interfaceType, out var serviceClient))
                return serviceClient;

            var contract = _serviceRegistry.GetServiceImplementingInterface(interfaceType);
            var serviceClientType = ServiceClientTypeProvider.GenerateServiceClientType(contract);
            var instance = Activator.CreateInstance(serviceClientType, new object[] { this });
            foreach (var interfaceModel in contract.Interfaces)
                _serviceClients[interfaceModel.Interface] = instance;
            return instance;
        }

        private static async Task HandleIncomingRequest<TRequest, TResponse>(Autobus autobus, Delegate messageHandler, ReadOnlyMemory<byte> data, object sender)
        {
            var deserialized = autobus._serializationProvider.Deserialize<TRequest>(data);
            var resp = await ((OnRequestDelegate<TRequest, TResponse>)messageHandler)(deserialized);
            autobus._serializationProvider.Serialize(resp, (Transport: autobus._transport, Sender: sender), (data, state) =>
            {
                state.Transport.Publish(state.Sender, data);
            });
        }

        private static Task HandleIncomingMessage<TCommand>(Autobus autobus, Delegate messageHandler, ReadOnlyMemory<byte> data)
        {
            var deserialized = autobus._serializationProvider.Deserialize<TCommand>(data);
            return ((OnMessageDelegate<TCommand>)messageHandler)(deserialized);
        }
    }
}
