using SlimBus.Enums;
using SlimBus.Abstractions;

namespace SlimBus.Implementations
{
    public record ServiceExchangeModel(IServiceContract ServiceContract, ExchangeType ExchangeType);
}
