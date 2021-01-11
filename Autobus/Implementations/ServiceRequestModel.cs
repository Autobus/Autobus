using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Autobus.Implementations
{
    public record ServiceRequestModel(int RequestId, TaskCompletionSource<ServiceResponseModel> ResponseTask);
}
