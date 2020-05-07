using Abb.CqrsEs.DI;
using System;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class CqrsEsServiceCollectionExtensions
    {
        public static IServiceCollection AddCqrsEs(this IServiceCollection services, Action<ICqrsEsBuilder> build)
        {
            var builder = new CqrsEsBuilder<IServiceCollection>(services ?? throw new ArgumentNullException(nameof(services)));

            (build ?? throw new ArgumentNullException(nameof(build)))(builder);

            return builder.Build();
        }
    }
}