using System;

namespace Autobus.Models
{
    public record ServiceResponseModel(ReadOnlyMemory<byte> Data, object Sender);
}
