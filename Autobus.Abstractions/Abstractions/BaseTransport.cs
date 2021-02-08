using Autobus.Delegates;
using Autobus.Enums;
using Autobus.Types;
using Autobus.Models;

namespace Autobus.Abstractions
{
    public abstract class BaseTransport : ITransport
    {
        protected MessageHandlerDelegate MessageHandler { get; private set; }

        public void SetMessageHandler(MessageHandlerDelegate messageHandler) => MessageHandler = messageHandler;

        public abstract void DeclareService(IServiceContract service);
        public abstract void BindTo(IServiceContract service, MessageModel message);
        public abstract ServiceRequestModel CreateNewRequest(int requestId);
        public abstract bool DiscardRequest(ServiceRequestModel requestModel);
        public abstract void Dispose();
        public abstract void Publish(IServiceContract service, MessageModel message, ReadOnlySpanOrMemory<byte> data);
        public abstract void Publish(IServiceContract service, MessageModel message, ReadOnlySpanOrMemory<byte> data, ServiceRequestModel requestModel);
        public abstract void Publish(object sender, ReadOnlySpanOrMemory<byte> data);
        public abstract void UnbindFrom(IServiceContract service, MessageModel message);
        public abstract void Acknowledge(object sender);
        public abstract void Reject(object sender);
    }
}
