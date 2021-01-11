using System;
using System.Threading.Tasks;

namespace Autobus.Delegates
{
    public delegate Task MessageHandlerDelegate(string identity, ReadOnlyMemory<byte> data, object sender);
}
