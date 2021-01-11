namespace SlimBus.Transports.RabbitMQ
{
    public class RabbitMQTransportConfig
    {
        public bool UseConsistentHashing { get; set; }
        public uint PrefetchSize { get; set; } = 0;
        public ushort PrefetchCount { get; set; } = 10;
    }
}
