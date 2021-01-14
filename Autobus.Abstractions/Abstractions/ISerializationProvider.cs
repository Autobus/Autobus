using System;
using Autobus.Delegates;

namespace Autobus.Abstractions
{
    public interface ISerializationProvider
    {
        void Serialize<TMessage, TState>(TMessage message, TState state, OnSerializedDelegate<TState> onSerialized);

        T Deserialize<T>(ReadOnlyMemory<byte> data);
    }
}
