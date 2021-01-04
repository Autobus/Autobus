using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using SlimBus.Enums;
using SlimBus.Interfaces;

namespace SlimBus
{
    public record MessageModel(Type Type, MessageBehavior Behavior);
}
