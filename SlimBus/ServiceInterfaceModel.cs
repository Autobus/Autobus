using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;

namespace SlimBus
{
    public record ServiceInterfaceModel(
        IReadOnlyList<ServiceInterfaceModel.Request> Requests,
        IReadOnlyList<ServiceInterfaceModel.Command> Commands,
        IReadOnlyList<ServiceInterfaceModel.EventHandler> EventHandlers)
    {
        public record Request(Type RequestType, Type ResponseType, MethodInfo RequestHandler);

        public record Command(Type CommandType, MethodInfo CommandHandler);
        
        public record EventHandler(Type EventType, EventInfo Handler);
        
        public static Type? ExtractEventType(EventInfo eventInfo)
        {
            return null;
        }

        public static (Type RequestType, Type ResponseType)? ExtractRequestTypes(MethodInfo methodInfo)
        {
            if (methodInfo.IsGenericMethod)
                return null;
            
            var parameters = methodInfo.GetParameters();
            if (parameters.Length == 0)
                return null;
            var requestType = parameters[0].ParameterType;
            
            var responseType = methodInfo.ReturnType;
            if (responseType.IsGenericType)
            {
                var genericTypeDefinition = responseType.GetGenericTypeDefinition();
                if (!(genericTypeDefinition == typeof(Task<>) ||
                      genericTypeDefinition == typeof(ValueTask<>)))
                    return null;

                var genericArguments = genericTypeDefinition.GetGenericArguments();
                if (genericArguments.Length != 1)
                    return null;

                responseType = genericArguments[0];
            }

            return (requestType, responseType);
        }

        public static Type? ExtractCommandType(MethodInfo methodInfo)
        {
            if (methodInfo.IsGenericMethod)
                return null;
            var returnType = methodInfo.ReturnType;
            if (!(returnType == null || returnType == typeof(Task) || returnType == typeof(ValueTask)))
                return null;
            var methodParameters = methodInfo.GetParameters();
            return methodParameters.Length != 0 ? null : methodParameters[0].ParameterType;
        }
        
        public static ServiceInterfaceModel FromInterface(Type interfaceType)
        {
            if (!interfaceType.IsInterface) throw new ArgumentException();

            var requests = new List<Request>();
            var commands = new List<Command>();
            var eventHandlers = new List<EventHandler>();

            foreach (var eventInfo in interfaceType.GetEvents())
            {
                var eventType = ExtractEventType(eventInfo);
                if (eventType != null) eventHandlers.Add(new (eventType, eventInfo));
            }

            foreach (var methodInfo in interfaceType.GetMethods())
            {
                if (methodInfo.IsSpecialName)
                    continue;
                
                var commandType = ExtractCommandType(methodInfo);
                if (commandType != null)
                {
                    commands.Add(new (commandType, methodInfo));
                    continue;
                }

                var requestTypes = ExtractRequestTypes(methodInfo);
                if (requestTypes != null)
                {
                    var values = requestTypes.Value;
                    requests.Add(new(values.RequestType, values.ResponseType, methodInfo));
                }
            }

            return new (requests, commands, eventHandlers);
        }
        
        public static ServiceInterfaceModel FromInterface<T>() => FromInterface(typeof(T));
    }
}