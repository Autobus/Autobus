using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Autobus.Enums;
using Autobus.Abstractions;

namespace Autobus
{
    public record MessageModel(Type Type, MessageBehavior Behavior);
}
