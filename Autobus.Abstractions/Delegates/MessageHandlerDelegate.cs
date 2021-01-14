using System;
using System.Threading.Tasks;

namespace Autobus.Delegates
{
    public delegate void MessageHandlerDelegate(string name, ReadOnlyMemory<byte> data, object sender);
}
