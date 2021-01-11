using SlimBus.Abstractions;
using System;

namespace SlimBus.Transports.RabbitMQ
{
    public static class ISlimBusBuilderExtensions
    {
        public static ISlimBusBuilder UseRabbitMQTransport(this ISlimBusBuilder builder, Action<RabbitMQTransportBuilder> onBuild) => 
            builder.UseTransport(onBuild);
    }
}
