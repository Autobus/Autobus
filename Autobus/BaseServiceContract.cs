using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Autobus.Abstractions;
using Autobus.Models;

namespace Autobus
{
    public abstract class BaseServiceContract : IServiceContract
    {
        public string Name { get; internal set; }
        
        public IReadOnlyList<ServiceInterfaceModel> Interfaces { get; internal set; }
        
        public ReadOnlyDictionary<Type, Type> Requests { get; internal set; }
        
        public IReadOnlyList<MessageModel> Messages { get; internal set; }
        
        public abstract void Build(IServiceContractBuilder builder);

        public Type GetResponseType(Type requestType) => Requests[requestType];
    }
}