using Autobus.Types;
using System;
using Autobus.Models;

namespace Autobus.Abstractions
{
    public interface ITransport : IDisposable
    {
        void BindTo(IServiceContract service, MessageModel message);

        void UnbindFrom(IServiceContract service, MessageModel message);

        void Publish(IServiceContract service, MessageModel message, ReadOnlySpanOrMemory<byte> data);

        void Publish(IServiceContract service, MessageModel message, ReadOnlySpanOrMemory<byte> data, ServiceRequestModel requestModel);

        void Publish(object sender, ReadOnlySpanOrMemory<byte> data);

        void Acknowledge(object sender);

        void Reject(object sender);

        ServiceRequestModel CreateNewRequest(int requestId);

        bool DiscardRequest(ServiceRequestModel requestModel);
    }
}
