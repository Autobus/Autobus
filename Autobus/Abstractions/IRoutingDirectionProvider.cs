using Autobus.Enums;
using Autobus.Implementations;

namespace Autobus.Abstractions
{
    public interface IRoutingDirectionProvider
    {
        string GetExchangeName(ServiceExchangeModel exchangeModel);

        string GetMessageRoutingKey(MessageModel messageModel);
    }
}
