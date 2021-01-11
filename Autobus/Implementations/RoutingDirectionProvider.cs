﻿using Autobus.Enums;
using Autobus.Abstractions;

namespace Autobus.Implementations
{
    public class RoutingDirectionProvider : IRoutingDirectionProvider
    {
        public string GetExchangeName(ServiceExchangeModel exchangeModel)
        {
            return exchangeModel.ExchangeType switch
            {
                ExchangeType.Topic => exchangeModel.ServiceContract.Name,
                ExchangeType.Fanout => $"{exchangeModel.ServiceContract.Name}.events",
                _ => throw new System.NotImplementedException(),
            };
        }

        public string GetMessageRoutingKey(MessageModel messageModel) => messageModel.Type.Name;
    }
}