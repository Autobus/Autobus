using SlimBus.Implementations;

namespace SlimBus.Delegates
{
    public delegate void OnSerializedDelegate<TState>(ReadOnlySpanOrMemory<byte> data, TState state);
}
