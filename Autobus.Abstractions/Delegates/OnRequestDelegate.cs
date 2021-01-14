using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Autobus.Delegates
{
    public delegate Task<TResponse> OnRequestDelegate<in TRequest, TResponse>(TRequest message);
}
