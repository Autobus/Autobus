using System;
using System.Threading.Tasks;
using Autobus.Abstractions;
using Autobus.Types;
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
        
        private readonly RabbitMQProducerChannelPool _producerChannelPool;

        private readonly ConcurrentDictionary<int, ServiceRequestModel> _pendingRequests = new();

        private readonly Dictionary<string, int> _serviceQueueRefs = new();

        private readonly IConnection _producingConnection;
        
        private readonly IConnection _consumingConnection;

        private readonly (IModel Channel, string QueueName, ActionBasicConsumer Consumer) _consumptionChannel;

        private readonly (IModel Channel, string QueueName, ActionBasicConsumer Consumer) _replyChannel;
        
        public RabbitMQTransport(
            RabbitMQTransportConfig config, 
            IConnection producingConnection, 
            IConnection consumingConnection)
        {
            _config = config;
            
            // Setup our normal event and request consumption
            _consumingConnection = consumingConnection;
            var consumptionChannel = _consumingConnection.CreateModel();
            var consumptionQueue = consumptionChannel.QueueDeclare();
            var messageConsumer = new ActionBasicConsumer(consumptionChannel)
            {
                Received = OnIncomingPacket
            };
            //consumptionChannel.BasicQos(_config.PrefetchSize, _config.PrefetchCount, false);
            consumptionChannel.BasicConsume(messageConsumer, consumptionQueue, autoAck: false);
            _consumptionChannel = (consumptionChannel, consumptionQueue, messageConsumer);
            
            // Setup our reply consumption
            var replyChannel = _consumingConnection.CreateModel();
            var replyQueue = replyChannel.QueueDeclare();
            var replyConsumer = new ActionBasicConsumer(replyChannel)
            {
                Received = OnIncomingResponse
            };
            replyChannel.BasicConsume(replyConsumer, replyQueue, autoAck: true);
            Console.WriteLine($"Consuming replies on: {replyQueue.QueueName}");
            _replyChannel = (replyChannel, replyQueue, replyConsumer);

            // Setup our producing
            _producingConnection = producingConnection;
            _producerChannelPool = new RabbitMQProducerChannelPool(_producingConnection, config.ProducerChannelPoolSize);
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
                _consumptionChannel.Channel.ExchangeDeclare(
                    exchange: exchange,
                    type: "x-consistent-hash",
                    durable: false,
                    autoDelete: true,
                    arguments: new Dictionary<string, object>() {["hash-header"] = "hash-on"});
                _consumptionChannel.Channel.QueueBind(
                    _consumptionChannel.QueueName, 
                    exchange, 
                    routingKey);
            }
            else
            {
                _consumptionChannel.Channel.ExchangeDeclare(
                    exchange: exchange,
                    type: ExchangeType.Direct,
                    durable: false,
                    autoDelete: false);
                var queueName = GetServiceRequestQueueName(service);
                if (!_serviceQueueRefs.TryGetValue(queueName, out var refs))
                {
                    _consumptionChannel.Channel.QueueDeclare(
                        queue: queueName,
                        durable: true,
                        exclusive: false,
                        autoDelete: false);
                    _consumptionChannel.Channel.BasicConsume(_consumptionChannel.Consumer, queueName, false);
                    _serviceQueueRefs[queueName] = 0;
                }
                _consumptionChannel.Channel.QueueBind(queueName, exchange, routingKey);
                _serviceQueueRefs[queueName] = refs + 1;
            }
        }

        private void BindToEvent(IServiceContract service, MessageModel message)
        {
            var (exchange, routingKey) = GetRoutingInfo(service, message);
            _consumptionChannel.Channel.ExchangeDeclare(
                exchange: exchange,
                type: ExchangeType.Direct,
                durable: false,
                autoDelete: false);
            _consumptionChannel.Channel.QueueBind(
                _consumptionChannel.QueueName, 
                exchange, 
                routingKey);
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
                _consumptionChannel.Channel.QueueUnbind(_consumptionChannel.QueueName, exchange, routingKey);
            }
            else
            {
                var queueName = GetServiceRequestQueueName(service);
                var refs = _serviceQueueRefs[queueName] = _serviceQueueRefs[queueName] - 1;
                if (refs == 0)
                {
                    // TODO: Stop consuming on the consumer tag we created
                    _serviceQueueRefs.Remove(queueName);
                }
            }
        }

        private void UnbindFromEvent(IServiceContract service, MessageModel message)
        {
            var (exchange, routingKey) = GetRoutingInfo(service, message);
            _consumptionChannel.Channel.QueueUnbind(
                _consumptionChannel.QueueName, 
                exchange, 
                routingKey);
        }

        public override void Publish(IServiceContract service, MessageModel message, ReadOnlySpanOrMemory<byte> data)
        {
            // Have to make a memory allocation in case we were passed a span, rabbit mq is expecting memory
            var passableData = data.IsMemory ? data.Memory : data.Span.ToArray();
            var (exchange, routingKey) = GetRoutingInfo(service, message);
            _producerChannelPool.BasicPublish(exchange, routingKey, passableData);
        }

        public override void Publish(IServiceContract service, MessageModel message, ReadOnlySpanOrMemory<byte> data, ServiceRequestModel requestModel)
        {
            // Have to make a memory allocation in case we were passed a span, rabbit mq is expecting memory
            var passableData = data.IsMemory ? data.Memory : data.Span.ToArray();
            var (exchange, routingKey) = GetRoutingInfo(service, message);
            _producerChannelPool.BasicPublish(exchange, routingKey, _replyChannel.QueueName, requestModel.RequestId, passableData);
        }

        public override void Publish(object sender, ReadOnlySpanOrMemory<byte> data)
        {
            if (!(sender is BasicDeliverEventArgs ea))
                throw new Exception();
            var replyAddress = ea.BasicProperties.ReplyTo;
            var correlationId = int.Parse(ea.BasicProperties.CorrelationId);
            var passableData = data.IsMemory ? data.Memory : data.Span.ToArray();
            _producerChannelPool.BasicPublish(replyAddress, correlationId, passableData);
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

        private void OnIncomingResponse(object sender, BasicDeliverEventArgs ea)
        {
            // If we have a correlation id we are dealing with a response.
            if (int.TryParse(ea.BasicProperties.CorrelationId, out var correlationId) &&
                _pendingRequests.TryRemove(correlationId, out var requestModel))
            {
                var responseModel = new ServiceResponseModel(ea.Body.ToArray(), ea);
                requestModel.CompletionSource.SetResult(responseModel);
            }
        }
        
        private void OnIncomingPacket(object sender, BasicDeliverEventArgs ea)
        {
            MessageHandler(ea.RoutingKey, ea.Body.ToArray(), ea);
        }

        public override void Dispose()
        {
            //_channel.Dispose();
            _consumingConnection.Dispose();
            _producingConnection.Dispose();
        }

        public override void Acknowledge(object sender)
        {
            if (!(sender is BasicDeliverEventArgs ea))
                throw new Exception();
            _consumptionChannel.Channel.BasicAck(ea.DeliveryTag, false);
        }

        public override void Reject(object sender)
        {
            if (!(sender is BasicDeliverEventArgs ea))
                throw new Exception();
            _consumptionChannel.Channel.BasicReject(ea.DeliveryTag, true);
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
