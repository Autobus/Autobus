namespace SlimBus.Abstractions
{
    public interface ICorrelationIdProvider
    {
        int GetNextCorrelationId();
    }
}
