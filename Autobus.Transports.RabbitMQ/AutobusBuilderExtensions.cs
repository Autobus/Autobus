using Autobus.Transports.RabbitMQ;
using System;

namespace Autobus
{
    public static class AutobusBuilderExtensions
    {
        public static IAutobusBuilder UseRabbitMQTransport(this IAutobusBuilder builder, Action<RabbitMQTransportBuilder> onBuild) => 
            builder.UseTransport(onBuild);
    }
}
