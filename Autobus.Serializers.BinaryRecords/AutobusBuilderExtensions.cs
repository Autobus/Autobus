using Autobus.Serialization.BinaryRecords;

namespace Autobus
{
    public static class AutobusBuilderExtensions
    {
        public static IAutobusBuilder UseBinaryRecordsSerialization(this IAutobusBuilder builder) => builder.UseSerializer<BinaryRecordsSerializationProvider>();
    }
}
