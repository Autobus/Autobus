using System;
using System.Threading.Tasks;
using Autobus.Abstractions;
using Autobus.Implementations;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Autobus.Enums;
using Autobus.Models;

namespace Autobus.Transports.RabbitMQ
{
    public class RabbitMQTransport : BaseTransport
    {
        private readonly RabbitMQTransportConfig _config;

        private readonly AsyncEventingBasicConsumer _consumer;

        private ConcurrentDictionary<int, ServiceRequestModel> _pendingRequests = new();

        private Dictionary<string, int> _serviceQueueRefs = new();

        private IConnection _connection;

        private IModel _channel;

        private string _queueName;

        public RabbitMQTransport(RabbitMQTransportConfig config, IConnection connection, IModel channel, string queueName)
        {
            _config = config;
            _connection = connection;
            _channel = channel;
            _queueName = queueName;

            // Start consuming packets on the queue
            _consumer = new AsyncEventingBasicConsumer(_channel);
            _consumer.Received += OnIncomingPacket;
            _channel.BasicQos(_config.PrefetchSize, _config.PrefetchCount, false);
            _channel.BasicConsume(_queueName, false, _consumer);
        }

        private static string GetServiceRequestQueueName(IServiceContract service) => $"{service.Name}.Requests";

        public override void BindTo(IServiceContract service, MessageModel message)
        {
            if (message.Behavior is (MessageBehavior.Request or MessageBehavior.Command))
                BindToRequest(service, message);
            else if (message.Behavior is MessageBehavior.Event)
                BindToEvent(service, message);
            else
                throw new Exception();
        }

        private void BindToRequest(IServiceContract service, MessageModel message)
        {
            var (exchange, routingKey) = GetRoutingInfo(service, message);
            if (_config.UseConsistentHashing)
            {
                _channel.ExchangeDeclare(
                    exchange: exchange,
                    type: "x-consistent-hash",
                    durable: false,
                    autoDelete: true,
                    arguments: new Dictionary<string, object>() {["hash-header"] = "hash-on"});
                _channel.QueueBind(_queueName, exchange, routingKey);
            }
            else
            {
                _channel.ExchangeDeclare(
                    exchange: exchange,
                    type: ExchangeType.Direct,
                    durable: false,
                    autoDelete: false);
                var queueName = GetServiceRequestQueueName(service);
                if (!_serviceQueueRefs.TryGetValue(queueName, out var refs))
                {
                    var queue = _channel.QueueDeclare(
                        queue: queueName,
                        durable: true,
                        exclusive: false,
                        autoDelete: false);
                    _channel.BasicConsume(queue, false, _consumer);
                    _serviceQueueRefs[queueName] = 0;
                }
                _channel.QueueBind(queueName, exchange, routingKey);
                _serviceQueueRefs[queueName] = refs + 1;
            }
        }

        private void BindToEvent(IServiceContract service, MessageModel message)
        {
            var (exchange, routingKey) = GetRoutingInfo(service, message);
            _channel.ExchangeDeclare(
                exchange: exchange,
                type: ExchangeType.Direct,
                durable: false,
                autoDelete: false);
            _channel.QueueBind(_queueName, exchange, routingKey);
        }
        
        public override void UnbindFrom(IServiceContract service, MessageModel message)
        {
            if (message.Behavior is (MessageBehavior.Request or MessageBehavior.Command))
                UnbindFromRequest(service, message);
            else if (message.Behavior is MessageBehavior.Event)
                UnbindFromEvent(service, message);
            else
                throw new Exception();
        }

        private void UnbindFromRequest(IServiceContract service, MessageModel message)
        {
            if (_config.UseConsistentHashing)
            {
                var (exchange, routingKey) = GetRoutingInfo(service, message);
                _channel.QueueUnbind(_queueName, exchange, routingKey);
            }
            else
            {
                var queueName = GetServiceRequestQueueName(service);
                var refs = _serviceQueueRefs[queueName] = _serviceQueueRefs[queueName] - 1;
                if (refs == 0)
                {
                    _channel.QueueDelete(
                        queue: queueName,
                        ifUnused: true);
                    _serviceQueueRefs.Remove(queueName);
                }
            }
        }

        private void UnbindFromEvent(IServiceContract service, MessageModel message)
        {
            var (exchange, routingKey) = GetRoutingInfo(service, message);
            _channel.QueueUnbind(_queueName, exchange, routingKey);
        }

        public override void Publish(IServiceContract service, MessageModel message, ReadOnlySpanOrMemory<byte> data)
        {
            var props = _channel.CreateBasicProperties();
            // Have to make a memory allocation in case we were passed a span, rabbit mq is expecting memory
            var passableData = data.IsMemory ? data.Memory : data.Span.ToArray();
            var (exchange, routingKey) = GetRoutingInfo(service, message);
            _channel.BasicPublish(exchange, routingKey, props, passableData);
        }

        public override void Publish(IServiceContract service, MessageModel message, ReadOnlySpanOrMemory<byte> data, ServiceRequestModel requestModel)
        {
            var props = _channel.CreateBasicProperties();
            props.ReplyTo = _queueName;
            props.CorrelationId = requestModel.RequestId.ToString();
            // Have to make a memory allocation in case we were passed a span, rabbit mq is expecting memory
            var passableData = data.IsMemory ? data.Memory : data.Span.ToArray();
            var (exchange, routingKey) = GetRoutingInfo(service, message);
            _channel.BasicPublish(exchange, routingKey, props, passableData);
        }

        public override void Publish(object sender, ReadOnlySpanOrMemory<byte> data)
        {
            if (!(sender is BasicDeliverEventArgs ea))
                throw new Exception();
            var props = _channel.CreateBasicProperties();
            props.CorrelationId = ea.BasicProperties.CorrelationId;
            var replyAddress = ea.BasicProperties.ReplyTo;
            var passableData = data.IsMemory ? data.Memory : data.Span.ToArray();
            _channel.BasicPublish("", replyAddress, props, passableData);
        }

        public override ServiceRequestModel CreateNewRequest(int requestId)
        {
            var responseTask = new TaskCompletionSource<ServiceResponseModel>();
            var model = new ServiceRequestModel(requestId, responseTask);
            if (!_pendingRequests.TryAdd(requestId, model))
                throw new Exception(); // duplicate request id
            return model;
        }

        public override bool DiscardRequest(ServiceRequestModel requestModel)
        {
            return _pendingRequests.TryRemove(requestModel.RequestId, out _);
        }

        private async Task OnIncomingPacket(object sender, BasicDeliverEventArgs ea)
        {
            // If we have a correlation id we are dealing with a response.
            if (int.TryParse(ea.BasicProperties.CorrelationId, out var correlationId) &&
                _pendingRequests.TryRemove(correlationId, out var requestModel))
            {
                var responseModel = new ServiceResponseModel(ea.Body, ea);
                requestModel.ResponseTask.SetResult(responseModel);
                return;
            }

            try
            {
                // Get the routing key and try to handle it
                await MessageHandler(ea.RoutingKey, ea.Body, ea);
                _channel.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception e)
            {
                // TODO: do something with the exception
                _channel.BasicReject(ea.DeliveryTag, false);
                throw;
            }
        }

        public override void Dispose()
        {
            _channel.Dispose();
            _connection.Dispose();
        }

        public override void Acknowledge(object sender)
        {
            if (!(sender is BasicDeliverEventArgs ea))
                throw new Exception();
            _channel.BasicAck(ea.DeliveryTag, false);
        }

        public override void Reject(object sender)
        {
            if (!(sender is BasicDeliverEventArgs ea))
                throw new Exception();
            _channel.BasicReject(ea.DeliveryTag, true);
        }

        private static (string Exchange, string RoutingKey) GetRoutingInfo(IServiceContract service, MessageModel message) =>
            message.Behavior switch
            {
                MessageBehavior.Request or MessageBehavior.Command => (service.Name, message.Name),
                MessageBehavior.Event => ($"{service.Name}.Events", message.Name),
                _ => throw new Exception($"Unroutable message: {message}")
            };
    }
}
