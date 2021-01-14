using System;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Autobus.Transports.RabbitMQ
{
    public class ActionBasicConsumer : DefaultBasicConsumer
    {
        public ActionBasicConsumer(IModel model) : base(model)
        {
        }
        
        public Action<object, BasicDeliverEventArgs> Received;

        ///<summary>
        /// Invoked when a delivery arrives for the consumer.
        /// </summary>
        /// <remarks>
        /// Handlers must copy or fully use delivery body before returning.
        /// Accessing the body at a later point is unsafe as its memory can
        /// be already released.
        /// </remarks>
        public override void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, ReadOnlyMemory<byte> body)
        {
            base.HandleBasicDeliver(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body);
            Received(this, 
                new BasicDeliverEventArgs(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body));
        }
    }
}