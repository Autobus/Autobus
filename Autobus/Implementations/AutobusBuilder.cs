using System;
using System.Linq;
using System.Collections.Generic;
using Autobus.Abstractions;
using Autobus.Implementations;
using System.Reflection;
using System.Runtime.CompilerServices;
using Autobus.Abstractions.Abstractions;
using Autobus.Enums;

namespace Autobus
{
    public class AutobusBuilder : IAutobusBuilder
    {
        private BaseTransport _transport;

        private ICorrelationIdProvider? _correlationIdProvider;

        private ISerializationProvider? _serializationProvider;

        private IAutobusLogger? _logger;

        private List<IServiceContract> _serviceContracts = new();
        
        private AutobusConfig _config = new();

        public IAutobusBuilder UseRequestTimeout(int milliseconds)
        {
            _config.RequestTimeout = milliseconds;
            return this;
        }

        public IAutobusBuilder UseService(IServiceContract serviceContract)
        {
            if (_serviceContracts.Any(c => c.Name == serviceContract.Name))
                throw new Exception();
            _serviceContracts.Add(serviceContract);
            return this;
        }

        public IAutobusBuilder UseService<T>() where T : BaseServiceContract, new() =>
            UseService(ServiceContractBuilder.Build<T>());

        public IAutobusBuilder UseServicesFromAssembly(Assembly assembly)
        {
            var genericBuildMethod = typeof(ServiceContractBuilder).GetMethods()
                .First(m => m.IsGenericMethod && m.IsStatic && m.Name == "Build");
            var contractTypes = assembly.GetTypes()
                .Where(t => t.BaseType == typeof(BaseServiceContract));
            foreach (var contractType in contractTypes)
            {
                if (contractType == typeof(AnonymousServiceContract))
                    continue;
                var builderMethod = genericBuildMethod.MakeGenericMethod(contractType);
                var contract = (IServiceContract)builderMethod.Invoke(null, null);
                UseService(contract);
            }
            return this;
        }

        public IAutobusBuilder UseServicesFromAllAssemblies()
        {
            static void LoadAssemblyReferences(Assembly assembly)
            {
                foreach (var name in assembly.GetReferencedAssemblies())
                    if (AppDomain.CurrentDomain.GetAssemblies().All(a => a.FullName != name.FullName))
                        LoadAssemblyReferences(Assembly.Load(name));
            }
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                LoadAssemblyReferences(assembly);
            foreach (var loadedAssembly in AppDomain.CurrentDomain.GetAssemblies())
                UseServicesFromAssembly(loadedAssembly);
            return this;
        }

        public IAutobusBuilder UseLogger(IAutobusLogger logger)
        {
            if (_logger != null)
                throw new Exception("Already defined a logger");
            _logger = logger;
            return this;
        }

        public IAutobusBuilder UseSerializer(ISerializationProvider serializationProvider)
        {
            _serializationProvider = serializationProvider;
            return this;
        }

        public IAutobusBuilder UseTransport<TTransportBuilder>(Action<TTransportBuilder> onBuild)
            where TTransportBuilder : ITransportBuilder, new()
        {
            if (_transport != null)
                throw new Exception("Already defined a transport!");
            var builder = new TTransportBuilder();
            onBuild(builder);
            _transport = builder.Build();
            return this;
        }

        public IAutobusBuilder UseCorrelationIdProvider(ICorrelationIdProvider correlationIdProvider) 
        {
            _correlationIdProvider = correlationIdProvider;
            return this;
        }

        public IAutobus Build()
        {
            if (_transport == null)
                throw new Exception("No transport defined!");
            if (_serializationProvider == null)
                throw new Exception("No serialization provider defined!");
            _correlationIdProvider ??= new CorrelationIdProvider();
            _logger ??= new AutobusConsoleLogger();
            var serviceContractRegistry = new ServiceRegistry(_serviceContracts);
            var autobus = new Autobus(_config, _logger, serviceContractRegistry, _serializationProvider, _correlationIdProvider, _transport);
            _transport.SetMessageHandler(autobus.HandleMessage);
            return autobus;
        }
    }
}
