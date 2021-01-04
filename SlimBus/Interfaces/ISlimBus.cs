using System.Threading.Tasks;

namespace SlimBus.Interfaces
{
    public interface ISlimBus
    {
        void Publish<TMessage>(TMessage message);

        Task<TResponse> Publish<TRequest, TResponse>(TRequest request);
    }
}