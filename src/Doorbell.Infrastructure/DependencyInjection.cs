using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services)
        {
            services.AddScoped<
                IDoorbellRingEventRepository,
                DoorbellRingEventRepository>();

            services.AddScoped<
                IDoorbellCompositionRepository,
                DoorbellCompositionRepository>();

            services.AddScoped<
                IUnitOfWork,
                UnitOfWork>();

            return services;
        }
    }
}
