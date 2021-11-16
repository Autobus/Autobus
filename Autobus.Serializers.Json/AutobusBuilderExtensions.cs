using Autobus.Serialization.Json;

namespace Autobus
{
    public static class AutobusBuilderExtensions
    {
        public static IAutobusBuilder UseJsonSerialization(this IAutobusBuilder builder) =>
            builder.UseSerializer<JsonSerializationProvider>();
    }
}
