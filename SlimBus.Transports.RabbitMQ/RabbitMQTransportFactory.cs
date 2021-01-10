using RabbitMQ.Client;
using SlimBus.Abstractions;
using SlimBus.Abstractions;

namespace SlimBus.Transports.RabbitMQ
{
    public class RabbitMQTransportFactory : ITransportFactory<RabbitMQTransportConfig>
    {
        public BaseTransport Create(RabbitMQTransportConfig config)
        {
            var factory = new ConnectionFactory()
            {
                UserName = config.Username,
                Password = config.Password,
                VirtualHost = config.VirtualHost,
                HostName = config.HostName
            };
            var connection = factory.CreateConnection();
            var channel = connection.CreateModel();
            var queueName = channel.QueueDeclare();
            return new RabbitMQTransport(connection, channel, queueName) { Config = config };
        }
    }
}
