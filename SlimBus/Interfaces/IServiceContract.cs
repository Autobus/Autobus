using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SlimBus.Interfaces
{
    public interface IServiceContract
    {
        public string Name { get; }
        public IReadOnlyList<ServiceInterfaceModel> Interfaces { get; }
        public ReadOnlyDictionary<Type, Type> Requests { get; }
        public IReadOnlyList<MessageContract> Messages { get; }
    }
}