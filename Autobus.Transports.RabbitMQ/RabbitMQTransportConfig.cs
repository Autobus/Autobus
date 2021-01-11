namespace Autobus.Transports.RabbitMQ
{
    public class RabbitMQTransportConfig
    {
        public bool UseConsistentHashing { get; set; } = false;
        public uint PrefetchSize { get; set; } = 0;
        public ushort PrefetchCount { get; set; } = 1;
    }
}
