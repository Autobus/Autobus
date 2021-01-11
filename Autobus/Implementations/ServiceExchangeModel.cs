using Autobus.Enums;
using Autobus.Abstractions;

namespace Autobus.Implementations
{
    public record ServiceExchangeModel(IServiceContract ServiceContract, ExchangeType ExchangeType);
}
