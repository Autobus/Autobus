using System;
using Autobus.Abstractions;
using Autobus.Delegates;
using BinaryRecords;

namespace Autobus.Serialization.BinaryRecords
{
    public class BinaryRecordsSerializationProvider : ISerializationProvider
    {
        public T Deserialize<T>(ReadOnlyMemory<byte> data) => BinarySerializer.Deserialize<T>(data.Span);

        public void Serialize<TMessage, TState>(TMessage message, TState state, OnSerializedDelegate<TState> onSerialized) =>
            BinarySerializer.Serialize(message, (state, onSerialized), (data, state) => state.onSerialized(data, state.state));
    }
}
