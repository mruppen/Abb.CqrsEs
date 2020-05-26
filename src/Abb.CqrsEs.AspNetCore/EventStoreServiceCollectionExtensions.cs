using Abb.CqrsEs.Internal;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Abb.CqrsEs.AspNetCore
{
    public static class EventStoreServiceCollectionExtensions
    {
        public static IServiceCollection AddEventSourcing(this IServiceCollection services)
        {
            return services.AddSingleton<IAggregateFactory, DefaultAggregateFactory>()
                .AddSingleton<IAggregateRepository, AggregateRepository>()
                .AddSingleton<CreateAggregateRootDelegate>(p => p.GetService);
        }
    }
}