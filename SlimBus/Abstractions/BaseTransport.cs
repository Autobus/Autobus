using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SlimBus.Abstractions;
using SlimBus.Delegates;
using SlimBus.Implementations;
using SlimBus.Enums;

namespace SlimBus.Abstractions
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

    public abstract class BaseTransport<TConfig> : BaseTransport, ITransport<TConfig> where TConfig: class
    {
        public TConfig Config { get; init; }
    }
}
