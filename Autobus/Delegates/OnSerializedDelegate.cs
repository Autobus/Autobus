using Autobus.Implementations;

namespace Autobus.Delegates
{
    public delegate void OnSerializedDelegate<TState>(ReadOnlySpanOrMemory<byte> data, TState state);
}
