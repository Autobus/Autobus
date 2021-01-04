using SlimBus.Abstractions;
using SlimBus.Implementations;

namespace SlimBus.Interfaces
{
    public interface ISlimBusBuilder
    {
        ISlimBusBuilder UseService(IServiceContract serviceContract);
        
        ISlimBusBuilder UseService<T>() where T : BaseServiceContract, new() =>
            UseService(ServiceContractBuilder.Build<T>());
    }
}
