using SlimBus.Abstractions;
using System;

namespace SlimBus.Transports.RabbitMQ
{
    public static class ISlimBusBuilderExtensions
    {
        public static ISlimBusBuilder UseRabbitMQTransport(this ISlimBusBuilder builder, Action<RabbitMQTransportConfig> onConfig)
        {
            var config = new RabbitMQTransportConfig();
            onConfig(config);
            return builder.UseTransport<RabbitMQTransportFactory, RabbitMQTransportConfig>(config);
        }

        public static ISlimBusBuilder UseRabbitMQTransport(this ISlimBusBuilder builder, RabbitMQTransportConfig config) =>
            builder.UseTransport<RabbitMQTransportFactory, RabbitMQTransportConfig>(config);
    }
}
