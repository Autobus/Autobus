using SlimBus.Abstractions;
using System.Threading;

namespace SlimBus.Implementations
{
    public class CorrelationIdProvider : ICorrelationIdProvider
    {
        private int _current = 0;

        public int GetNextCorrelationId() => Interlocked.Increment(ref _current);
    }
}
