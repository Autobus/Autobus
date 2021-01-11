using System;
using System.Linq;
using System.Collections.Generic;
using SlimBus.Abstractions;
using SlimBus.Implementations;

namespace SlimBus
{
    public class SlimBusBuilder : ISlimBusBuilder
    {
        private BaseTransport _transport;

        private ICorrelationIdProvider? _correlationIdProvider;

        private IRoutingDirectionProvider? _routingDirectionProvider;

        private ISerializationProvider? _serializationProvider;

        private List<IServiceContract> _serviceContracts = new();

        public ISlimBusBuilder UseService(IServiceContract serviceContract)
        {
            if (_serviceContracts.Any(c => c.Name == serviceContract.Name))
                throw new Exception();
            _serviceContracts.Add(serviceContract);
            return this;
        }

        public ISlimBusBuilder UseServicesFromAllAssemblies()
        {
            var genericBuildMethod = typeof(ServiceContractBuilder).GetMethods()
                .First(m => m.IsGenericMethod && m.IsStatic && m.Name == "Build");
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var loadedAssembly in loadedAssemblies)
            {
                var contractTypes = loadedAssembly.GetTypes().Where(t => t.BaseType == typeof(BaseServiceContract));
                foreach (var contractType in contractTypes)
                {
                    if (contractType == typeof(AnonymousServiceContract))
                        continue;
                    var builderMethod = genericBuildMethod.MakeGenericMethod(contractType);
                    var contract = (IServiceContract)builderMethod.Invoke(null, null);
                    UseService(contract);
                }
            }
            return this;
        }

        public ISlimBusBuilder UseSerializer(ISerializationProvider serializationProvider)
        {
            _serializationProvider = serializationProvider;
            return this;
        }

        public ISlimBusBuilder UseTransport<TTransportBuilder>(Action<TTransportBuilder> onBuild)
            where TTransportBuilder : ITransportBuilder, new()
        {
            if (_transport != null)
                throw new Exception("Already defined a transport!");
            var builder = new TTransportBuilder();
            onBuild(builder);
            _transport = builder.Build();
            return this;
        }

        public ISlimBusBuilder UseCorrelationIdProvider(ICorrelationIdProvider correlationIdProvider) 
        {
            _correlationIdProvider = correlationIdProvider;
            return this;
        }

        public ISlimBusBuilder UseRoutingDirectionProvider(IRoutingDirectionProvider routingDirectionProvider)
        {
            _routingDirectionProvider = routingDirectionProvider;
            return this;
        }

        public ISlimBus Build()
        {
            if (_transport == null)
                throw new Exception("No transport defined!");
            if (_serializationProvider == null)
                throw new Exception("No serialization provider defined!");
            var serviceContractRegistry = new ServiceRegistry(_serviceContracts);
            _correlationIdProvider ??= new CorrelationIdProvider();
            _routingDirectionProvider ??= new RoutingDirectionProvider();

            // TODO: Figure out if we should declare exchanges here or on demand
            foreach (var model in serviceContractRegistry.GetExchangeModels())
            {
                var name = _routingDirectionProvider.GetExchangeName(model);
                _transport.DeclareExchange(name, model.ExchangeType);
            }

            var slimBus = new SlimBus(serviceContractRegistry, _serializationProvider, _routingDirectionProvider, _correlationIdProvider, _transport);
            _transport.SetMessageHandler(slimBus.HandleMessage);
            return slimBus;
        }
    }
}
