using System;
using System.Threading.Tasks;
using SlimBus.Abstractions;
using SlimBus.Implementations;
using SlimBus.Abstractions;
using SlimBus.Delegates;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SlimBus.Transports.RabbitMQ
{
    public class RabbitMQTransportConfig
    { 
        public string Username { get; set; }
        public string Password { get; set; }
        public string VirtualHost { get; set; }
        public string HostName { get; set; }
        public bool UseConsistentHashing { get; set; }
    }

    public class RabbitMQTransport : BaseTransport<RabbitMQTransportConfig>
    {
        private readonly EventingBasicConsumer _consumer;

        private readonly Dictionary<string, int> _boundServiceQueueRefs = new();

        private ConcurrentDictionary<int, ServiceRequestModel> _pendingRequests = new();

        private IConnection _connection;

        private IModel _channel;

        private string _queueName;

        public RabbitMQTransport(IConnection connection, IModel channel, string queueName)
        {
            _connection = connection;
            _channel = channel;
            _queueName = queueName;

            // Start consuming packets on the queue
            _consumer = new EventingBasicConsumer(_channel);
            _consumer.Received += OnRecieved;
            
            // TODO: Config options for qos and auto ack
            _channel.BasicQos(0, 1, false);
            _channel.BasicConsume(_queueName, false, _consumer);
        }

        private void OnRecieved(object sender, BasicDeliverEventArgs ea)
        {
            Task.Run(async () =>
            {
                try
                {
                    await OnIncomingPacket(sender, ea);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            });
        }

        public override void DeclareExchange(string name, Enums.ExchangeType type)
        {


            if (Config.UseConsistentHashing)
            {
                _channel.ExchangeDeclare(
                    exchange: name, 
                    type: "x-consistent-hash", 
                    durable: false, 
                    autoDelete: true, 
                    arguments: new Dictionary<string, object>() { ["hash-header"] = "hash-on" });
            }
            else
            {
                var rabbitType = type switch
                {
                    Enums.ExchangeType.Topic => ExchangeType.Topic,
                    Enums.ExchangeType.Fanout => ExchangeType.Fanout,
                    _ => throw new NotImplementedException()
                };
                Console.WriteLine($"Declare exchange {name}, {rabbitType}");
                _channel.ExchangeDeclare(
                    exchange: name, 
                    type: rabbitType);
            }
        }

        private static string GetExchangeRequestQueueName(string exchange) => $"{exchange}.Requests";

        public override void BindTo(string exchange, string routingKey)
        {
            if (Config.UseConsistentHashing)
            {
                _channel.QueueBind(_queueName, exchange, routingKey);
            }
            else
            {
                var queueName = GetExchangeRequestQueueName(exchange);
                if (_boundServiceQueueRefs.TryGetValue(queueName, out var refs))
                {
                    _channel.QueueBind(queueName, exchange, routingKey);
                    _boundServiceQueueRefs[queueName] = refs + 1;
                    return;
                }

                var queue = _channel.QueueDeclare(
                    queue: queueName,
                    durable: true, 
                    exclusive: false,
                    autoDelete: false);

                // TODO: Setup auto ack
                _channel.QueueBind(queueName, exchange, routingKey);
                _channel.BasicConsume(queue, false, _consumer);
                _boundServiceQueueRefs[queueName] = 1;
            }
        }

        public override void UnbindFrom(string exchange, string routingKey)
        {
            if (Config.UseConsistentHashing)
            {
                _channel.QueueUnbind(_queueName, exchange, routingKey);
            }
            else
            {
                var queueName = GetExchangeRequestQueueName(exchange);
                var refs = _boundServiceQueueRefs[queueName] = _boundServiceQueueRefs[queueName] - 1;
                if (refs == 0)
                {
                    _channel.QueueDelete(
                        queue: queueName,
                        ifUnused: true);
                    _boundServiceQueueRefs.Remove(queueName);
                }
            }
        }

        public override void Publish(string exchange, string routingKey, ReadOnlySpanOrMemory<byte> data)
        {
            var props = _channel.CreateBasicProperties();
            // Have to make a memory allocation in case we were passed a span, rabbit mq is expecting memory
            var passableData = data.IsMemory ? data.Memory : data.Span.ToArray();
            _channel.BasicPublish(exchange, routingKey, props, passableData);
        }

        public override void Publish(string exchange, string routingKey, ReadOnlySpanOrMemory<byte> data, ServiceRequestModel requestModel)
        {
            var props = _channel.CreateBasicProperties();
            props.ReplyTo = _queueName;
            props.CorrelationId = requestModel.RequestId.ToString();
            // Have to make a memory allocation in case we were passed a span, rabbit mq is expecting memory
            var passableData = data.IsMemory ? data.Memory : data.Span.ToArray();
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
    }
}
