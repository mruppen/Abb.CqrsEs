using BaselineTypeDiscovery;
using Lamar;
using Lamar.Scanning.Conventions;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Abb.CqrsEs.DI.Lamar
{
    internal class CqrsEsBuilderLamar : CqrsEsBuilder<ServiceRegistry>
    {
        public CqrsEsBuilderLamar(ServiceRegistry services) : base(services)
        {
        }

        protected override void RegisterEventHandlers() => _services.Scan(scan =>
                                                         {
                                                             scan.AssembliesAndExecutablesFromApplicationBaseDirectory(a => !a.IsDynamic);

                                                             scan.AddAllTypesOf(typeof(IEventHandler<>));
                                                             scan.AddAllTypesOf(typeof(ICommandHandler<>));

                                                             scan.With(new Convention(typeof(IEventHandler<>)));
                                                             scan.With(new Convention(typeof(ICommandHandler<>)));
                                                         });

        private class Convention : IRegistrationConvention
        {
            private readonly Type _openGenericInterface;

            public Convention(Type openGenericInterface)
            {
                _openGenericInterface = openGenericInterface;
            }

            public void ScanTypes(TypeSet types, ServiceRegistry services)
            {
                var openHandlers = types.FindTypes(TypeClassification.Concretes | TypeClassification.Open);
                openHandlers.ForEach(h =>
                {
                    if (h.ImplementsInterface(_openGenericInterface))
                    {
                        services.Add(ServiceDescriptor.Transient(_openGenericInterface, h));
                    }
                });
            }
        }
    }
}