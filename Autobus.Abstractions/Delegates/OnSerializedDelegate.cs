using Autobus.Types;

namespace Autobus.Delegates
{
    public delegate void OnSerializedDelegate<in TState>(ReadOnlySpanOrMemory<byte> data, TState state);
}
