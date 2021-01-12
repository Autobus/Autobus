using System;
using Autobus.Serialization.BinaryRecords;
using BinaryRecords;

namespace Autobus
{
    public static class AutobusBuilderExtensions
    {
        public static IAutobusBuilder UseBinaryRecordsSerialization(this IAutobusBuilder builder, Action<BinarySerializerBuilder> onSerializerBuild)
        {
            var serializerBuilder = new BinarySerializerBuilder();
            onSerializerBuild(serializerBuilder);
            var serializer = serializerBuilder.Build();
            return builder.UseSerializer(new BinaryRecordsSerializationProvider(serializer));
        }

        public static IAutobusBuilder UseBinaryRecordsSerialization(this IAutobusBuilder builder) => builder.UseSerializer<BinaryRecordsSerializationProvider>();
    }
}
