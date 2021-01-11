using Autobus.Abstractions;
using System;

namespace Autobus.Transports.RabbitMQ
{
    public static class IAutobusBuilderExtensions
    {
        public static IAutobusBuilder UseRabbitMQTransport(this IAutobusBuilder builder, Action<RabbitMQTransportBuilder> onBuild) => 
            builder.UseTransport(onBuild);
    }
}
