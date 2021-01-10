using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SlimBus.Delegates
{
    public delegate Task<TResponse> OnRequestDelegate<TRequest, TResponse>(TRequest message);
}
