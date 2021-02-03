using RabbitMQ.Client;
using Autobus.Abstractions;
using System;

namespace Autobus.Transports.RabbitMQ
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

        public RabbitMQTransportBuilder ConfigureProducerChannelPoolSize(int producerChannelPoolSize)
        {
            _config.ProducerChannelPoolSize = producerChannelPoolSize;
            return this;
        }

        public BaseTransport Build()
        {
            _connectionFactory.AutomaticRecoveryEnabled = true;
            var producingConnection = _connectionFactory.CreateConnection();
            var consumingConnection = _connectionFactory.CreateConnection();
            return new RabbitMQTransport(_config, producingConnection, consumingConnection);
        }
    }
}
