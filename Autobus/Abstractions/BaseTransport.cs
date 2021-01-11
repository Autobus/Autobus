using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autobus.Abstractions;
using Autobus.Delegates;
using Autobus.Implementations;
using Autobus.Enums;

namespace Autobus.Abstractions
{
    public abstract class BaseTransport : ITransport
    {
        protected MessageHandlerDelegate MessageHandler { get; private set; }

        internal void SetMessageHandler(MessageHandlerDelegate messageHandler) => MessageHandler = messageHandler;

        public abstract void BindTo(string exchange, string routingKey);
        public abstract ServiceRequestModel CreateNewRequest(int requestId);
        public abstract void DeclareExchange(string name, ExchangeType type);
        public abstract bool DiscardRequest(ServiceRequestModel requestModel);
        public abstract void Dispose();
        public abstract void Publish(string exchange, string routingKey, ReadOnlySpanOrMemory<byte> data);
        public abstract void Publish(string exchange, string routingKey, ReadOnlySpanOrMemory<byte> data, ServiceRequestModel requestModel);
        public abstract void Publish(object sender, ReadOnlySpanOrMemory<byte> data);
        public abstract void UnbindFrom(string exchange, string routingKey);
        public abstract void Acknowledge(object sender);
        public abstract void Reject(object sender);
    }
}
