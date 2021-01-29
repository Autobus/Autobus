using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Autobus.Models
{
    public record ServiceRequestModel(int RequestId, TaskCompletionSource<ServiceResponseModel> CompletionSource);
}
