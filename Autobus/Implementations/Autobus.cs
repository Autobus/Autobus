using System;
using System.Threading.Tasks;
using System.Linq.Expressions;
using Autobus.Abstractions;
using Autobus.Delegates;
using Autobus.Enums;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Autobus.Abstractions.Abstractions;
using Autobus.Implementations;
using Autobus.Providers;
using Autobus.Models;

namespace Autobus
{
    public class Autobus : IAutobus
    {
        private readonly AutobusConfig _config;
        
        private readonly IAutobusLogger _logger;
        
        private readonly IServiceRegistry _serviceRegistry;

        private readonly ISerializationProvider _serializationProvider;

        private readonly ICorrelationIdProvider _correlationIdProvider;

        private readonly ITransport _transport;

        private readonly Dictionary<Type, object> _serviceClients = new();

        private readonly Dictionary<Type, Delegate> _messageSubscriptions = new();

        private readonly Dictionary<Type, List<Delegate>> _eventSubscriptions = new();

        private readonly Dictionary<Type, nint> _messageHandlerFuncs = new();

        internal Autobus(
            AutobusConfig config, 
            IAutobusLogger logger, 
            IServiceRegistry serviceRegistry, 
            ISerializationProvider serializationProvider, 
            ICorrelationIdProvider correlationIdProvider, 
            ITransport transport)
        {
            _config = config;
            _logger = logger;
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
            _serializationProvider.Serialize(message, 
                (Transport: _transport, Service: service, Message: messageModel, Request: request), 
                (data, state) =>
            {
                state.Transport.Publish(state.Service, state.Message, data, state.Request);
            });
            return WaitForResponse<TResponse>(request);
        }

        private async Task<TResponse> WaitForResponse<TResponse>(ServiceRequestModel request)
        {
            // Setup our timeout
            var cts = new CancellationTokenSource();
            cts.CancelAfter(_config.RequestTimeout);
            cts.Token.Register(() =>
            {
                if (request.CompletionSource.Task.IsCompleted) 
                    return;
                request.CompletionSource.SetException(
                    new TimeoutException($"Timed out waiting for: {typeof(TResponse).FullName}"));
            });
            
            ServiceResponseModel response;
            try
            {
                response = await request.CompletionSource.Task.ConfigureAwait(false);
                cts.Cancel(); // Cancel the timeout
            }
            catch (Exception e)
            {
                _transport.DiscardRequest(request);
                throw;
            }
            finally
            {
                cts.Dispose();
            }

            return _serializationProvider.Deserialize<TResponse>(response.Data);
        }

        internal void HandleMessage(string name, ReadOnlyMemory<byte> data, object sender)
        {
            Task.Run(async () =>
            {
                var messageModel = _serviceRegistry.GetMessageModel(name);
                try
                {
                    await CallMessageHandler(name, data, sender);
                    if (messageModel.Behavior is MessageBehavior.Request or MessageBehavior.Command)
                        _transport.Acknowledge(sender);
                }
                catch (Exception e)
                {
                    _logger.Error(e);
                    if (messageModel.Behavior is MessageBehavior.Request or MessageBehavior.Command)
                        _transport.Reject(sender);
                }
            });
        }
        
        private unsafe Task CallMessageHandler(string name, ReadOnlyMemory<byte> data, object sender)
        {
            var messageModel = _serviceRegistry.GetMessageModel(name);
            if (!_messageHandlerFuncs.TryGetValue(messageModel.Type, out var handlerPtr))
                throw new Exception($"Don't know how to handle message: {messageModel}");
            return messageModel.Behavior switch
            {
                MessageBehavior.Request => 
                    ((delegate*<Autobus, ReadOnlyMemory<byte>, object, Task>) handlerPtr)(this, data, sender),
                MessageBehavior.Command or MessageBehavior.Event =>
                    ((delegate*<Autobus, ReadOnlyMemory<byte>, Task>) handlerPtr)(this, data),
                _ => throw new NotImplementedException()
            };
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
            delegate*<Autobus, ReadOnlyMemory<byte>, object, Task> handlerPtr = &HandleIncomingRequest<TRequest, TResponse>;
            _messageHandlerFuncs[requestModel.Type] = (nint)handlerPtr;
            BindTo<TRequest>();
        }

        public void Unsubscribe<TRequest, TResponse>(OnRequestDelegate<TRequest, TResponse> onRequest) => 
            Unsubscribe(typeof(TRequest), onRequest);

        public unsafe void Subscribe<TMessage>(OnMessageDelegate<TMessage> onMessage)
        {
            var message = _serviceRegistry.GetMessageModel<TMessage>();
            if (message.Behavior == MessageBehavior.Command)
            {
                if (!_messageSubscriptions.TryAdd(message.Type, onMessage))
                    throw new Exception($"Already bound to command type: {message.Name}");
                delegate*<Autobus, ReadOnlyMemory<byte>, Task> handlerPtr = &HandleIncomingCommand<TMessage>;
                _messageHandlerFuncs[message.Type] = (nint)handlerPtr;
            }
            else if (message.Behavior == MessageBehavior.Event)
            {
                if (!_eventSubscriptions.TryGetValue(message.Type, out var subscriptions))
                    subscriptions = _eventSubscriptions[message.Type] = new();
                if (subscriptions.Contains(onMessage))
                    throw new Exception($"Tried binding event handler more than once");
                subscriptions.Add(onMessage);
                delegate*<Autobus, ReadOnlyMemory<byte>, Task> handlerPtr = &HandleIncomingEvent<TMessage>;
                _messageHandlerFuncs[message.Type] = (nint)handlerPtr;
            }
            else
            {
                throw new Exception($"Cant bind to message: {message}");
            }
            BindTo<TMessage>();
        }

        public void Unsubscribe<TCommand>(OnMessageDelegate<TCommand> onCommand) => 
            Unsubscribe(typeof(TCommand), onCommand);

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

        public void Bind<TServiceContract>(object obj) where TServiceContract: IServiceContract => 
            Bind(obj, _serviceRegistry.GetServiceContract<TServiceContract>());

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
                    var bindRequestMethod = typeof(Autobus).GetMethod("BindObjectRequestHandler")
                        .MakeGenericMethod(new[] { requestModel.RequestType, requestModel.ResponseType });
                    bindRequestMethod.Invoke(this, new object[] { obj, requestModel.RequestHandler });
                }
                foreach (var commandModel in interfaceModel.Commands)
                {
                    var bindCommandMethod = typeof(Autobus).GetMethod("BindObjectCommandHandler")
                        .MakeGenericMethod(new[] { commandModel.CommandType });
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
            _logger.Debug($"Binding to {service.Name}, {message.Name}");
            _transport.BindTo(service, message);
        }

        private void BindTo<TMessage>() => BindTo(typeof(TMessage));

        private void UnbindFrom(Type messageType)
        {
            var message = _serviceRegistry.GetMessageModel(messageType);
            var service = _serviceRegistry.GetOwningService(message);
            _logger.Debug($"Unbinding from {service.Name}, {message.Name}");
            _transport.UnbindFrom(service, message);
        }

        private void UnbindFrom<TMessage>() => UnbindFrom(typeof(TMessage));

        public IServiceContract GetServiceContract(Type serviceContractType) => 
            _serviceRegistry.GetServiceContract(serviceContractType);

        public IReadOnlyList<IServiceContract> GetServiceContracts() => _serviceRegistry.GetServiceContracts();

        public IServiceContract? GetContractImplementingInterface(Type interfaceType) => 
            _serviceRegistry.GetServiceImplementingInterface(interfaceType);

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

        private static async Task HandleIncomingRequest<TRequest, TResponse>(Autobus autobus, ReadOnlyMemory<byte> data, object sender)
        {
            if (!autobus._messageSubscriptions.TryGetValue(typeof(TRequest), out var messageHandler))
                throw new Exception($"Got request {typeof(TRequest).FullName} with no bound handler.");    
            var deserialized = autobus._serializationProvider.Deserialize<TRequest>(data);
            var resp = await ((OnRequestDelegate<TRequest, TResponse>) messageHandler)(deserialized).ConfigureAwait(false);
            autobus._serializationProvider.Serialize(resp, (Transport: autobus._transport, Sender: sender), 
                (data, state) =>
            {
                state.Transport.Publish(state.Sender, data);
            });
        }

        private static Task HandleIncomingCommand<TCommand>(Autobus autobus, ReadOnlyMemory<byte> data)
        {
            if (!autobus._messageSubscriptions.TryGetValue(typeof(TCommand), out var messageHandler))
                throw new Exception($"Got command {typeof(TCommand).FullName} with no bound handler.");    
            var deserialized = autobus._serializationProvider.Deserialize<TCommand>(data);
            return ((OnMessageDelegate<TCommand>) messageHandler)(deserialized);
        }

        private static Task HandleIncomingEvent<TEvent>(Autobus autobus, ReadOnlyMemory<byte> data)
        {
            if (!autobus._eventSubscriptions.TryGetValue(typeof(TEvent), out var subscribers))
                throw new Exception($"Got event {typeof(TEvent).FullName} with no subscribers.");    
            var deserialized = autobus._serializationProvider.Deserialize<TEvent>(data);
            if (subscribers.Count == 1)
                return ((OnMessageDelegate<TEvent>) subscribers[0])(deserialized);
            var tasks = new Task[subscribers.Count];
            for (var i = 0; i < subscribers.Count; i++)
                tasks[i] = ((OnMessageDelegate<TEvent>) subscribers[i])(deserialized);
            return Task.WhenAll(tasks);
        }
    }
}
