using RabbitMQ.Client;
using SlimBus.Abstractions;
using System;

namespace SlimBus.Transports.RabbitMQ
{
    public class RabbitMQTransportBuilder : ITransportBuilder
    {
        private readonly RabbitMQTransportConfig _config = new();

        private readonly ConnectionFactory _connectionFactory = new();

        public RabbitMQTransportBuilder ConfigureConnectionFactory(Action<ConnectionFactory> onConfigure)
        {
            onConfigure(_connectionFactory);
            return this;
        }

        public RabbitMQTransportBuilder UseConsistentHashing()
        {
            _config.UseConsistentHashing = true;
            return this;
        }

        public RabbitMQTransportBuilder ConfigureQos(uint prefetchSize, ushort prefetchCount)
        {
            _config.PrefetchSize = prefetchSize;
            _config.PrefetchCount = prefetchCount;
            return this;
        }

        public BaseTransport Build()
        {
            var connection = _connectionFactory.CreateConnection();
            var channel = connection.CreateModel();
            var queueName = channel.QueueDeclare();
            return new RabbitMQTransport(_config, connection, channel, queueName);
        }
    }
}
