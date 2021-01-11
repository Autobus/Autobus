using Autobus.Abstractions;
using System.Threading;

namespace Autobus.Implementations
{
    public class CorrelationIdProvider : ICorrelationIdProvider
    {
        private int _current = 0;

        public int GetNextCorrelationId() => Interlocked.Increment(ref _current);
    }
}
