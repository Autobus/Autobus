using System;

namespace Autobus.Implementations
{
    public record ServiceResponseModel(ReadOnlyMemory<byte> Data, object Sender);
}
