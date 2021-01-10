using System;
using BinaryRecords;

namespace SlimBus.Serialization.BinaryRecords
{
    public static class ISlimBusBuilderExtensions
    {
        public static ISlimBusBuilder UseBinaryRecordsSerialization(this ISlimBusBuilder builder, Action<BinarySerializerBuilder> onSerializerBuild)
        {
            var serializerBuilder = new BinarySerializerBuilder();
            onSerializerBuild(serializerBuilder);
            var serializer = serializerBuilder.Build();
            return builder.UseSerializer(new BinaryRecordsSerializationProvider(serializer));
        }

        public static ISlimBusBuilder UseBinaryRecordsSerialization(this ISlimBusBuilder builder) => builder.UseSerializer<BinaryRecordsSerializationProvider>();
    }
}
