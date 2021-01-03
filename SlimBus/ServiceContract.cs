using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using SlimBus.Enums;
using SlimBus.Interfaces;

namespace SlimBus
{
    public record MessageContract(Type Type, MessageBehavior Behavior);

    public abstract class BaseServiceContract : IServiceContract
    {
        public string Name { get; internal set; }
        
        public IReadOnlyList<ServiceInterfaceModel> Interfaces { get; }
        
        public ReadOnlyDictionary<Type, Type> Requests { get; internal set; }
        
        public IReadOnlyList<MessageContract> Messages { get; internal set; }
        
        public abstract void Build(IServiceContractBuilder builder);

        public Type GetResponseType(Type requestType) => Requests[requestType];
    }
}
