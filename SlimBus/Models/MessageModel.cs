using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using SlimBus.Enums;
using SlimBus.Abstractions;

namespace SlimBus
{
    public record MessageModel(Type Type, MessageBehavior Behavior);
}
