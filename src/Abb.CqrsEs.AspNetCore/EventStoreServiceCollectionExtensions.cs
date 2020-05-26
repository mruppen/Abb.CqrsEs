using Abb.CqrsEs.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Abb.CqrsEs.AspNetCore
{
    public static class EventStoreServiceCollectionExtensions
    {
        public static IServiceCollection AddEventSourcing(this IServiceCollection services)
            => services.AddSingleton<IAggregateFactory, DefaultAggregateFactory>()
                .AddSingleton<IAggregateRepository, AggregateRepository>()
                .AddSingleton<CreateAggregateRootDelegate>(p => p.GetService);
    }
}