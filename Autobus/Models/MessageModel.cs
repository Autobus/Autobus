using System;
using Autobus.Enums;

namespace Autobus
{
    public record MessageModel(Type Type, MessageBehavior Behavior)
    {
        public string Name => Type.FullName ?? throw new InvalidOperationException();
    }
}
