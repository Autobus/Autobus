using System;
using System.Threading.Tasks;

namespace SlimBus.Delegates
{
    public delegate Task MessageHandlerDelegate(string identity, ReadOnlyMemory<byte> data, object sender);
}
