using Autobus.Implementations;
using Autobus.Enums;
using System;

namespace Autobus.Abstractions
{
    public interface ITransport : IDisposable
    {
        void DeclareExchange(string name, ExchangeType type);

        void BindTo(string exchange, string routingKey);

        void UnbindFrom(string exchange, string routingKey);

        void Publish(string exchange, string routingKey, ReadOnlySpanOrMemory<byte> data);

        void Publish(string exchange, string routingKey, ReadOnlySpanOrMemory<byte> data, ServiceRequestModel requestModel);

        void Publish(object sender, ReadOnlySpanOrMemory<byte> data);

        void Acknowledge(object sender);

        void Reject(object sender);

        ServiceRequestModel CreateNewRequest(int requestId);

        bool DiscardRequest(ServiceRequestModel requestModel);
    }
}
