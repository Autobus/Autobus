using System;
using System.Threading.Tasks;
using System.Linq.Expressions;
using Autobus.Abstractions;
using Autobus.Delegates;
using Autobus.Enums;
using System.Collections.Generic;
using System.Reflection;
using Autobus.Providers;
using Autobus.Implementations;

namespace Autobus
{
    public class Autobus : IAutobus
    {
        private readonly IServiceRegistry _serviceRegistry;

        private readonly ISerializationProvider _serializationProvider;

        private readonly IRoutingDirectionProvider _routingDirectionProvider;

        private readonly ICorrelationIdProvider _correlationIdProvider;

        private readonly ITransport _transport;

        private readonly Dictionary<Type, object> _serviceClients = new();

        private readonly Dictionary<Type, Delegate> _messageSubscriptions = new();

        private readonly Dictionary<Type, List<Delegate>> _eventSubscriptions = new();

        private readonly Dictionary<Type, nint> _messageHandlerFuncs = new();

        private readonly Dictionary<string, Type> _messageIdentities = new();

        internal Autobus(IServiceRegistry serviceRegistry, ISerializationProvider serializationProvider, IRoutingDirectionProvider routingDirectionProvider, ICorrelationIdProvider correlationIdProvider, ITransport transport)
        {
            _serviceRegistry = serviceRegistry;
            _serializationProvider = serializationProvider;
            _routingDirectionProvider = routingDirectionProvider;
            _correlationIdProvider = correlationIdProvider;
            _transport = transport;
            foreach (var messageModel in _serviceRegistry.GetMessageModels())
                _messageIdentities[_routingDirectionProvider.GetMessageRoutingKey(messageModel)] = messageModel.Type;
        }

        public void Publish<TMessage>(TMessage message)
        {
            var messageExchange = _serviceRegistry.GetMessageExchange<TMessage>();
            var messageModel = _serviceRegistry.GetMessageModel<TMessage>();
            var exchangeName = _routingDirectionProvider.GetExchangeName(messageExchange);
            var routingKey = _routingDirectionProvider.GetMessageRoutingKey(messageModel);
            _serializationProvider.Serialize(message, (Transport: _transport, ExchangeName: exchangeName, RoutingKey: routingKey), (data, state) =>
            {
                state.Transport.Publish(state.ExchangeName, state.RoutingKey, data);
            });
        }

        public Task<TResponse> Publish<TRequest, TResponse>(TRequest message)
        {
            var messageExchange = _serviceRegistry.GetMessageExchange<TRequest>();
            var messageModel = _serviceRegistry.GetMessageModel<TRequest>();
            var exchangeName = _routingDirectionProvider.GetExchangeName(messageExchange);
            var routingKey = _routingDirectionProvider.GetMessageRoutingKey(messageModel);
            var requestId = _correlationIdProvider.GetNextCorrelationId();
            var requestModel = _transport.CreateNewRequest(requestId);
            _serializationProvider.Serialize(message, (Transport: _transport, ExchangeName: exchangeName, RoutingKey: routingKey, Request: requestModel), (data, state) =>
            {
                state.Transport.Publish(state.ExchangeName, state.RoutingKey, data, state.Request);
            });
            return WaitForRequestResponse<TResponse>(requestModel);
        }

        private async Task<TResponse> WaitForRequestResponse<TResponse>(ServiceRequestModel requestModel)
        {
            // TODO: Handle timeouts here. If we wait too long we call DiscardRequest on the transport
            var responseModel = await requestModel.ResponseTask.Task;
            try
            {
                var deserialized = _serializationProvider.Deserialize<TResponse>(responseModel.Data);
                _transport.Acknowledge(responseModel.Sender);
                return deserialized;
            }
            catch
            {
                // TODO: We probably want to do some more error stuff here
                _transport.Reject(responseModel.Sender);
                throw;
            }
        }

        internal unsafe Task HandleMessage(string identity, ReadOnlyMemory<byte> data, object sender)
        {
            if (!_messageIdentities.TryGetValue(identity, out var messageType))
                throw new Exception(); // TODO: Exception types
            var messageModel = _serviceRegistry.GetMessageModel(messageType);
            if (messageModel.Behavior == MessageBehavior.Request)
            {
                if (!_messageSubscriptions.TryGetValue(messageType, out var messageHandler))
                    throw new Exception(); // TODO: Exception types
                var handlerPtr = (delegate*<Autobus, Delegate, ReadOnlyMemory<byte>, object, Task>)_messageHandlerFuncs[messageType];
                return handlerPtr(this, messageHandler, data, sender);
            }
            else if (messageModel.Behavior == MessageBehavior.Command)
            {
                if (!_messageSubscriptions.TryGetValue(messageType, out var messageHandler))
                    throw new Exception(); // TODO: Exception types
                var handlerPtr = (delegate*<Autobus, Delegate, ReadOnlyMemory<byte>, Task>)_messageHandlerFuncs[messageType];
                return handlerPtr(this, messageHandler, data);
            }
            else if (messageModel.Behavior == MessageBehavior.Event)
            {
                if (!_eventSubscriptions.TryGetValue(messageType, out var eventSubscripers))
                    throw new Exception();
                var handlerPtr = (delegate*<Autobus, Delegate, ReadOnlyMemory<byte>, Task>)_messageHandlerFuncs[messageType];
                if (eventSubscripers.Count == 1)
                    return handlerPtr(this, eventSubscripers[0], data);
                var eventTasks = new Task[eventSubscripers.Count];
                for (var i = 0; i < eventTasks.Length; i++)
                    eventTasks[i] = handlerPtr(this, eventSubscripers[i], data);
                return Task.WhenAll(eventTasks);
            }
            else
                throw new Exception();
        }

        public unsafe void Subscribe<TRequest, TResponse>(OnRequestDelegate<TRequest, TResponse> onRequest)
        {
            if (!CanSubscribeTo<TRequest, TResponse>())
                throw new Exception(); // TODO: Exception types
            var requestType = typeof(TRequest);
            if (!_messageSubscriptions.TryAdd(requestType, onRequest))
                throw new Exception();
            delegate*<Autobus, Delegate, ReadOnlyMemory<byte>, object, Task> handlerPtr = &HandleIncomingRequest<TRequest, TResponse>;
            _messageHandlerFuncs[requestType] = (nint)handlerPtr;
            BindTo<TRequest>();
        }

        public void Unsubscribe<TRequest, TResponse>(OnRequestDelegate<TRequest, TResponse> onRequest) => Unsubscribe(typeof(TRequest), onRequest);

        public unsafe void Subscribe<TMessage>(OnMessageDelegate<TMessage> onMessage)
        {
            if (!CanSubscribeTo<TMessage>())
                throw new Exception();
            var messageType = typeof(TMessage);
            var messageModel = _serviceRegistry.GetMessageModel<TMessage>();
            if (messageModel.Behavior == MessageBehavior.Command)
            {
                if (!_messageSubscriptions.TryAdd(messageType, onMessage))
                    throw new Exception();
            }
            else if (messageModel.Behavior == MessageBehavior.Event)
            {
                if (!_eventSubscriptions.TryGetValue(messageType, out var subscriptions))
                    subscriptions = _eventSubscriptions[messageType] = new();
                if (subscriptions.Contains(onMessage))
                    throw new Exception();
                subscriptions.Add(onMessage);
            }
            delegate*<Autobus, Delegate, ReadOnlyMemory<byte>, Task> handlerPtr = &HandleIncomingMessage<TMessage>;
            _messageHandlerFuncs[messageType] = (nint)handlerPtr;
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
            var messageExchange = _serviceRegistry.GetMessageExchange(messageType);
            var messageModel = _serviceRegistry.GetMessageModel(messageType);
            var routingKey = _routingDirectionProvider.GetMessageRoutingKey(messageModel);
            _transport.BindTo(_routingDirectionProvider.GetExchangeName(messageExchange), routingKey);
        }

        private void BindTo<TMessage>() => BindTo(typeof(TMessage));

        private void UnbindFrom(Type messageType)
        {
            var messageExchange = _serviceRegistry.GetMessageExchange(messageType);
            var messageModel = _serviceRegistry.GetMessageModel(messageType);
            var routingKey = _routingDirectionProvider.GetMessageRoutingKey(messageModel);
            _transport.UnbindFrom(_routingDirectionProvider.GetExchangeName(messageExchange), routingKey);
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

        private bool CanSubscribeTo<TRequest, TResponse>()
        {
            var serviceContract = _serviceRegistry.GetMessageExchange<TRequest>().ServiceContract;
            if (!serviceContract.Requests.TryGetValue(typeof(TRequest), out var responseType))
                return false;
            return responseType == typeof(TResponse);
        }

        private bool CanSubscribeTo<TMessage>()
        {
            var messageModel = _serviceRegistry.GetMessageModel<TMessage>();
            return messageModel.Behavior == MessageBehavior.Command || messageModel.Behavior == MessageBehavior.Event;
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
