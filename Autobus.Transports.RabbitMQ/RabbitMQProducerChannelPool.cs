using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using RabbitMQ.Client;

namespace Autobus.Transports.RabbitMQ
{
    using ProducerChannel = Channel<(string, string, IBasicProperties, ReadOnlyMemory<byte>)>;
    using ProducerChannelReader = ChannelReader<(string, string, IBasicProperties, ReadOnlyMemory<byte>)>;
    using ProducerChannelWriter = ChannelWriter<(string, string, IBasicProperties, ReadOnlyMemory<byte>)>;

    public class RabbitMQProducerChannel : IDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _producingTask;
        private readonly IModel _model;
        private readonly ProducerChannel _channel;
        private readonly ProducerChannelReader _channelReader;
        private readonly ProducerChannelWriter _channelWriter;
        
        private RabbitMQProducerChannel(IModel model, ProducerChannel channel)
        {
            _model = model;
            _channel = channel;
            _channelReader = _channel.Reader;
            _channelWriter = _channel.Writer;
            _producingTask = Task.Run(() => ProcessMessages(_cts.Token));
            _producingTask.ContinueWith(_ => _model.Dispose());
        }

        public void Write(string replyTo, int correlationId, ReadOnlyMemory<byte> data)
        {
            var properties = _model.CreateBasicProperties();
            properties.CorrelationId = correlationId.ToString();
            if (!_channelWriter.TryWrite(("", replyTo, properties, data)))
                throw new Exception(); // we only fail to write if the channel is closed
        }
        
        public void Write(string exchange, string routingKey, ReadOnlyMemory<byte> data)
        {
            var properties = _model.CreateBasicProperties();
            if (!_channelWriter.TryWrite((exchange, routingKey, properties, data)))
                throw new Exception(); // we only fail to write if the channel is closed
        }

        public void Write(string exchange, string routingKey, string replyTo, int correlationId,
            ReadOnlyMemory<byte> data)
        {
            var properties = _model.CreateBasicProperties();
            properties.ReplyTo = replyTo;
            properties.CorrelationId = correlationId.ToString();
            if (!_channelWriter.TryWrite((exchange, routingKey, properties, data)))
                throw new Exception(); // we only fail to write if the channel is closed
        }
        
        private async Task ProcessMessages(CancellationToken cancellationToken)
        {
            while (await _channelReader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (_channelReader.TryRead(out var publishable))
                    _model.BasicPublish(publishable.Item1, publishable.Item2, publishable.Item3, publishable.Item4);
            }
        }

        public void Dispose()
        {
            _channelWriter.Complete();
            _cts.Dispose();
        }

        public static RabbitMQProducerChannel FromConnection(IConnection connection)
        {
            var channelOptions = new UnboundedChannelOptions
            {
                SingleReader = true, 
                SingleWriter = false, 
                AllowSynchronousContinuations = false
            };
            var channel = Channel.CreateUnbounded<(string, string, IBasicProperties, ReadOnlyMemory<byte>)>(channelOptions);
            return new RabbitMQProducerChannel(connection.CreateModel(), channel);
        }
    }
    
    public class RabbitMQProducerChannelPool : IDisposable
    {
        private readonly RabbitMQProducerChannel[] _producerChannels;
        private int _producerChannelCounter = 0;

        public RabbitMQProducerChannelPool(IConnection connection, int channelCount)
        {
            if (channelCount > connection.ChannelMax)
                channelCount = connection.ChannelMax;
            _producerChannels = new RabbitMQProducerChannel[channelCount];
            for (var i = 0; i < _producerChannels.Length; i++)
                _producerChannels[i] = RabbitMQProducerChannel.FromConnection(connection);
        }
        
        public void BasicPublish(string replyTo, int correlationId, ReadOnlyMemory<byte> data) =>
            GetProducerChannel().Write(replyTo, correlationId, data);
        
        public void BasicPublish(string exchange, string routingKey, ReadOnlyMemory<byte> data) =>
            GetProducerChannel().Write(exchange, routingKey, data);

        public void BasicPublish(string exchange, string routingKey, string replyTo, int correlationId,
            ReadOnlyMemory<byte> data) =>
            GetProducerChannel().Write(exchange, routingKey, replyTo, correlationId, data);
        
        private RabbitMQProducerChannel GetProducerChannel()
        {
            var index = (int)(unchecked((uint)Interlocked.Increment(ref _producerChannelCounter)) % _producerChannels.Length);
            return _producerChannels[index];
        }

        public void Dispose()
        {
            for (var i = 0; i < _producerChannels.Length; i++)
            {
                _producerChannels[i].Dispose();
                _producerChannels[i] = null;
            }
        }
    }
}