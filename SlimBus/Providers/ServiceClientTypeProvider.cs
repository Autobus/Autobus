using SlimBus.Abstractions;
using SlimBus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace SlimBus.Providers
{
    public static class ServiceClientTypeProvider
    {
        // TODO: Type cacheing
        private static Dictionary<IServiceContract, Type> _serviceClientCache = new();

        private static AssemblyBuilder _assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName("SlimBus.Dynamic.ServiceClients"), AssemblyBuilderAccess.Run);

        private static ModuleBuilder _moduleBuilder = _assemblyBuilder.DefineDynamicModule("ServiceClients");

        private static string GenerateNameFromServiceContract(IServiceContract serviceContract) => $"{serviceContract.Name}ServiceClient";

        public static Type GenerateServiceClientType(IServiceContract serviceContract)
        {
            var typeName = GenerateNameFromServiceContract(serviceContract);
            var typeBuilder = _moduleBuilder.DefineType(
                typeName,
                TypeAttributes.Public | TypeAttributes.Sealed,
                typeof(object),
                serviceContract.Interfaces.Select(model => model.Interface).ToArray());
            var slimBusField = typeBuilder.DefineField("_slimBus", typeof(ISlimBus), FieldAttributes.Private);
            GenerateConstructor(typeBuilder, slimBusField);
            foreach (var interfaceModel in serviceContract.Interfaces)
            {
                foreach (var requestModel in interfaceModel.Requests)
                    GenerateRequestMethod(typeBuilder, slimBusField, requestModel);
                foreach (var commandModel in interfaceModel.Commands)
                    GenerateCommandMethod(typeBuilder, slimBusField, commandModel);
            }
            return typeBuilder.CreateType();
        }

        private static void GenerateConstructor(TypeBuilder typeBuilder, FieldInfo slimBusField)
        {
            var constructor = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.HasThis, new[] { typeof(ISlimBus) });
            var ilGenerator = constructor.GetILGenerator();
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Call, typeof(object).GetConstructor(Array.Empty<Type>()));
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Ldarg_1);
            ilGenerator.Emit(OpCodes.Stfld, slimBusField);
            ilGenerator.Emit(OpCodes.Ret);
        }
        private static void GenerateRequestMethod(TypeBuilder typeBuilder, FieldInfo slimBusField, ServiceInterfaceModel.Request requestModel)
        {
            var methodBuilder = typeBuilder.DefineMethod(
                requestModel.RequestHandler.Name,
                MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
                typeof(Task<>).MakeGenericType(requestModel.ResponseType),
                new Type[] { requestModel.RequestType });
            var ilGenerator = methodBuilder.GetILGenerator();
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Ldfld, slimBusField);

            var publishMethod = GetPublishMethod(requestModel.RequestType, requestModel.ResponseType);
            ilGenerator.Emit(OpCodes.Ldarg_1);
            ilGenerator.Emit(OpCodes.Callvirt, publishMethod);
            ilGenerator.Emit(OpCodes.Ret);
        }

        private static void GenerateCommandMethod(TypeBuilder typeBuilder, FieldInfo slimBusField, ServiceInterfaceModel.Command commandModel)
        {
            var methodBuilder = typeBuilder.DefineMethod(
                commandModel.CommandHandler.Name,
                MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
                typeof(Task),
                new Type[] { commandModel.CommandType });
            var ilGenerator = methodBuilder.GetILGenerator();
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Ldfld, slimBusField);

            var publishMethod = GetPublishMethod(commandModel.CommandType);
            ilGenerator.Emit(OpCodes.Ldarg_1);
            ilGenerator.Emit(OpCodes.Callvirt, publishMethod);

            var completedTaskGetter = typeof(Task).GetProperty("CompletedTask").GetMethod;
            ilGenerator.Emit(OpCodes.Call, completedTaskGetter);
            ilGenerator.Emit(OpCodes.Ret);            
        }

        private static MethodInfo GetPublishMethod(params Type[] genericArguments)
        {
            // For some reason the standard Type.GetMethod(string, int, Type[]) call returns null for our generic publish method
            var methodInfo = typeof(ISlimBus).GetMethods().FirstOrDefault(method =>
                method.IsGenericMethodDefinition &&
                method.Name == "Publish" &&
                method.GetGenericArguments().Length == genericArguments.Length);
            return methodInfo?.MakeGenericMethod(genericArguments);
        }
    }
}
