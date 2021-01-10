using System;

namespace SlimBus.Implementations
{
    public record ServiceResponseModel(ReadOnlyMemory<byte> Data, object Sender);
}
