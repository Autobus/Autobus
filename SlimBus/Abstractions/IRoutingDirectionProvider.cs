using SlimBus.Enums;
using SlimBus.Implementations;

namespace SlimBus.Abstractions
{
    public interface IRoutingDirectionProvider
    {
        string GetExchangeName(ServiceExchangeModel exchangeModel);

        string GetMessageRoutingKey(MessageModel messageModel);
    }
}
