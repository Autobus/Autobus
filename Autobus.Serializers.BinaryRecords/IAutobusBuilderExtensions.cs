using System;
using BinaryRecords;

namespace Autobus.Serialization.BinaryRecords
{
    public static class IAutobusBuilderExtensions
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
