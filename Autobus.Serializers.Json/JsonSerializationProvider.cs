using Autobus.Abstractions;
using Autobus.Delegates;
using Autobus.Types;
using System;
using System.Text.Json;

namespace Autobus.Serialization.Json
{
    public class JsonSerializationProvider : ISerializationProvider
    {
        public T Deserialize<T>(ReadOnlyMemory<byte> data) =>
            JsonSerializer.Deserialize<T>(data.Span)!;

        public void Serialize<TMessage, TState>(TMessage message, TState state, OnSerializedDelegate<TState> onSerialized)
        {
            var data = JsonSerializer.SerializeToUtf8Bytes(message);
            onSerialized(new ReadOnlySpanOrMemory<byte>(data.AsSpan()), state);
        }
    }
}
