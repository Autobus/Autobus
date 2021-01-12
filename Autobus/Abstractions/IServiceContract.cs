using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Autobus.Models;

namespace Autobus.Abstractions
{
    public interface IServiceContract
    {
        public string Name { get; }
        public IReadOnlyList<ServiceInterfaceModel> Interfaces { get; }
        public ReadOnlyDictionary<MessageModel, MessageModel> Requests { get; }
        public IReadOnlyList<MessageModel> Messages { get; }
    }
}