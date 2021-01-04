using System.Threading.Tasks;
using SlimBus.Interfaces;

namespace SlimBus.Implementations
{
    public class SlimBus : ISlimBus
    {
        public void Publish<TMessage>(TMessage message)
        {
            throw new System.NotImplementedException();
        }

        public Task<TResponse> Publish<TRequest, TResponse>(TRequest request)
        {
            throw new System.NotImplementedException();
        }
    }
}