namespace Autobus.Abstractions
{
    public interface ICorrelationIdProvider
    {
        int GetNextCorrelationId();
    }
}
