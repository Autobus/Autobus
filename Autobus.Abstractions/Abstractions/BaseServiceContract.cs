using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Autobus.Abstractions;
using Autobus.Models;

namespace Autobus
{
    public abstract class BaseServiceContract : IServiceContract
    {
        public string Name { get; set; }
        
        public IReadOnlyList<ServiceInterfaceModel> Interfaces { get; set; }
        
        public ReadOnlyDictionary<MessageModel, MessageModel> Requests { get; set; }
        
        public IReadOnlyList<MessageModel> Messages { get; set; }
        
        public abstract void Build(IServiceContractBuilder builder);
    }
}