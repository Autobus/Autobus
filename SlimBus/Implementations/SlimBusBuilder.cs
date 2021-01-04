using System;
using SlimBus.Interfaces;

namespace SlimBus.Implementations
{
    public class SlimBusBuilder : ISlimBusBuilder
    {
        public ISlimBusBuilder UseService(IServiceContract serviceContract)
        {
            throw new NotImplementedException();
        }
    }
}