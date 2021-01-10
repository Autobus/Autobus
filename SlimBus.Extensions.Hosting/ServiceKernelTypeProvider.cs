using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SlimBus.Abstractions;
using SlimBus.Models;
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;

namespace SlimBus.Extensions.Hosting
{
    public static class ServiceKernelTypeProvider
    {
        private static AssemblyBuilder _assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("SlimBus.Dynamic.ServiceKernels"), AssemblyBuilderAccess.Run);

        private static ModuleBuilder _moduleBuilder = _assemblyBuilder.DefineDynamicModule("ServiceKernels");

        private static string GenerateNameFromServiceContract(IServiceContract serviceContract) => $"{serviceContract.Name}ServiceKernel";

        public static Type GenerateServiceKernelType(Type kernelType, IServiceContract serviceContract)
        {
            var typeName = GenerateNameFromServiceContract(serviceContract);
            var interfaces = serviceContract.Interfaces
                .Select(model => model.Interface)
                .Append(typeof(IHostedService))
                .ToArray();
            var typeBuilder = _moduleBuilder.DefineType(
                typeName,
                TypeAttributes.Public | TypeAttributes.Sealed,
                typeof(object),
                interfaces);
            var serviceProviderField = typeBuilder.DefineField("_serviceProvider", typeof(IServiceProvider), FieldAttributes.Private);
            var serviceContractField = typeBuilder.DefineField("_serviceContract", typeof(IServiceContract), FieldAttributes.Private);
            var slimBusField = typeBuilder.DefineField("_slimBus", typeof(ISlimBus), FieldAttributes.Private);
            GenerateConstructor(typeBuilder, serviceContract.GetType(), serviceProviderField, serviceContractField, slimBusField);
            GenerateStartAsyncMethod(typeBuilder, serviceContractField, slimBusField);
            GenerateStopAsyncMethod(typeBuilder, slimBusField);
            foreach (var interfaceModel in serviceContract.Interfaces)
            {
                foreach (var requestModel in interfaceModel.Requests)
                    GenerateRequestMethod(typeBuilder, kernelType, serviceProviderField, requestModel);
                foreach (var commandModel in interfaceModel.Commands)
                    GenerateCommandMethod(typeBuilder, kernelType, serviceProviderField, commandModel);
            }
            return typeBuilder.CreateType();
        }

        private static void GenerateConstructor(TypeBuilder typeBuilder, Type serviceContractType, FieldInfo serviceProviderField, FieldInfo serviceContractField, FieldInfo slimBusField)
        {
            var constructor = typeBuilder.DefineConstructor(
                MethodAttributes.Public, CallingConventions.HasThis, 
                new[] { typeof(IServiceProvider), serviceContractType, typeof(ISlimBus) });
            var ilGenerator = constructor.GetILGenerator();
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Call, typeof(object).GetConstructor(Array.Empty<Type>()));
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Ldarg_1);
            ilGenerator.Emit(OpCodes.Stfld, serviceProviderField);
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Ldarg_2);
            ilGenerator.Emit(OpCodes.Stfld, serviceContractField);
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Ldarg_3);
            ilGenerator.Emit(OpCodes.Stfld, slimBusField);
            ilGenerator.Emit(OpCodes.Ret);
        }

        private static void GenerateStartAsyncMethod(TypeBuilder typeBuilder, FieldInfo serviceContractField, FieldInfo slimBusField)
        {
            var methodBuilder = typeBuilder.DefineMethod(
                "StartAsync",
                MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
                typeof(Task),
                new Type[] { typeof(CancellationToken) });
            var ilGenerator = methodBuilder.GetILGenerator();
            
            // Load the args for the bind method
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Ldfld, slimBusField);
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Ldfld, serviceContractField);

            // Call it
            var bindMethod = typeof(ISlimBus).GetMethod("Bind", new Type[] { typeof(object), typeof(IServiceContract) });
            ilGenerator.Emit(OpCodes.Callvirt, bindMethod);

            // Now return a completed task
            var completedTaskGetter = typeof(Task).GetProperty("CompletedTask").GetMethod;
            ilGenerator.Emit(OpCodes.Call, completedTaskGetter);
            ilGenerator.Emit(OpCodes.Ret);
        }

        private static void GenerateStopAsyncMethod(TypeBuilder typeBuilder, FieldInfo slimBusField)
        {
            var methodBuilder = typeBuilder.DefineMethod(
                "StopAsync",
                MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
                typeof(Task),
                new Type[] { typeof(CancellationToken) });
            var ilGenerator = methodBuilder.GetILGenerator();

            // Load the args for the unbind method
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Ldfld, slimBusField);
            ilGenerator.Emit(OpCodes.Ldarg_0);

            // Call it
            var bindMethod = typeof(ISlimBus).GetMethod("Unbind", new Type[] { typeof(object) });
            ilGenerator.Emit(OpCodes.Callvirt, bindMethod);

            // Now return a completed task
            var completedTaskGetter = typeof(Task).GetProperty("CompletedTask").GetMethod;
            ilGenerator.Emit(OpCodes.Call, completedTaskGetter);
            ilGenerator.Emit(OpCodes.Ret);
        }

        private static void GenerateRequestMethod(TypeBuilder typeBuilder, Type kernelType, FieldInfo serviceProviderField, ServiceInterfaceModel.Request requestModel)
        {
            var responseTaskType = typeof(Task<>).MakeGenericType(requestModel.ResponseType);
            var methodBuilder = typeBuilder.DefineMethod(
                requestModel.RequestHandler.Name,
                MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
                responseTaskType,
                new Type[] { requestModel.RequestType });
            var ilGenerator = methodBuilder.GetILGenerator();
            var scopeLocal = ilGenerator.DeclareLocal(typeof(IServiceScope));
            var serviceLocal = ilGenerator.DeclareLocal(kernelType);
            var responseLocal = ilGenerator.DeclareLocal(responseTaskType);

            // Create a new scope
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Ldfld, serviceProviderField);
            ilGenerator.Emit(OpCodes.Call, typeof(ServiceProviderServiceExtensions).GetMethod("CreateScope", new [] { typeof(IServiceProvider) }));
            ilGenerator.Emit(OpCodes.Stloc, scopeLocal);

            // Get the service scope
            ilGenerator.Emit(OpCodes.Ldloc, scopeLocal);
            ilGenerator.Emit(OpCodes.Callvirt, typeof(IServiceScope).GetProperty("ServiceProvider").GetMethod);

            // Now call the generic get required service on it
            ilGenerator.Emit(OpCodes.Call, GetRequiredServiceMethod(kernelType));
            ilGenerator.Emit(OpCodes.Stloc, serviceLocal);

            // Call the request method on the service
            ilGenerator.Emit(OpCodes.Ldloc, serviceLocal);
            ilGenerator.Emit(OpCodes.Ldarg_1);
            ilGenerator.Emit(OpCodes.Callvirt, requestModel.RequestHandler);
            ilGenerator.Emit(OpCodes.Stloc, responseLocal);

            // Now indicate the scope should be disposed after the task is complete
            ilGenerator.Emit(OpCodes.Ldloc, responseLocal);
            ilGenerator.Emit(OpCodes.Ldloc, scopeLocal);
            var continueWithDisposeMethod = typeof(ServiceKernelTypeProvider).GetMethod("ContinueWithScopeDispose");
            ilGenerator.Emit(OpCodes.Call, continueWithDisposeMethod);

            // Load response task and return
            ilGenerator.Emit(OpCodes.Ldloc, responseLocal);
            ilGenerator.Emit(OpCodes.Ret);
        }

        private static void GenerateCommandMethod(TypeBuilder typeBuilder, Type kernelType, FieldInfo serviceProviderField, ServiceInterfaceModel.Command commandModel)
        {
            var responseTaskType = typeof(Task);
            var methodBuilder = typeBuilder.DefineMethod(
                commandModel.CommandHandler.Name,
                MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
                responseTaskType,
                new Type[] { commandModel.CommandType });
            var ilGenerator = methodBuilder.GetILGenerator();
            var scopeLocal = ilGenerator.DeclareLocal(typeof(IServiceScope));
            var serviceLocal = ilGenerator.DeclareLocal(kernelType);
            var responseLocal = ilGenerator.DeclareLocal(responseTaskType);

            // Create a new scope
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Ldfld, serviceProviderField);
            ilGenerator.Emit(OpCodes.Call, typeof(ServiceProviderServiceExtensions).GetMethod("CreateScope", new[] { typeof(IServiceProvider) }));
            ilGenerator.Emit(OpCodes.Stloc, scopeLocal);

            // Get the service scope
            ilGenerator.Emit(OpCodes.Ldloc, scopeLocal);
            ilGenerator.Emit(OpCodes.Callvirt, typeof(IServiceScope).GetProperty("ServiceProvider").GetMethod);

            // Now call the generic get required service on it
            ilGenerator.Emit(OpCodes.Call, GetRequiredServiceMethod(kernelType));
            ilGenerator.Emit(OpCodes.Stloc, serviceLocal);

            // Call the command method on the service
            ilGenerator.Emit(OpCodes.Ldloc, serviceLocal);
            ilGenerator.Emit(OpCodes.Ldarg_1);
            ilGenerator.Emit(OpCodes.Callvirt, commandModel.CommandHandler);
            ilGenerator.Emit(OpCodes.Stloc, responseLocal);

            // Now indicate the scope should be disposed after the task is complete
            ilGenerator.Emit(OpCodes.Ldloc, responseLocal);
            ilGenerator.Emit(OpCodes.Ldloc, scopeLocal);
            var continueWithDisposeMethod = typeof(ServiceKernelTypeProvider).GetMethod("ContinueWithScopeDispose");
            ilGenerator.Emit(OpCodes.Call, continueWithDisposeMethod);

            // Load response task and return
            ilGenerator.Emit(OpCodes.Ldloc, responseLocal);
            ilGenerator.Emit(OpCodes.Ret);
        }

        private static MethodInfo GetRequiredServiceMethod(Type serviceType)
        {
            var method = typeof(ServiceProviderServiceExtensions).GetMethods()
                .FirstOrDefault(method => method.IsGenericMethod && method.Name == "GetRequiredService");
            return method.MakeGenericMethod(serviceType);
        }

        public static void ContinueWithScopeDispose(Task task, IServiceScope serviceScope)
        {
            task.ContinueWith(_ => serviceScope.Dispose());
        }
    }
}
