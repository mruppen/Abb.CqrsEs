using Abb.CqrsEs.DI.Lamar;
using Lamar;
using System;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class CqrsEsServiceRegistryExtensions
    {
        public static ServiceRegistry AddCqrsEs(this ServiceRegistry services, Action<ICqrsEsBuilder> build)
        {
            var builder = new CqrsEsBuilderLamar(services ?? throw new ArgumentNullException(nameof(services)));

            (build ?? throw new ArgumentNullException(nameof(build)))(builder);

            return builder.Build();
        }
    }
}
