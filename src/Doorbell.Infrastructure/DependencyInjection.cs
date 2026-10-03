using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Infrastructure.Messaging.Mqtt;
using Doorbell.Infrastructure.Messaging.Mqtt.Consumers;
using Doorbell.Infrastructure.Persistence;
using Doorbell.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace Doorbell.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services , IConfiguration configuration)
        {
            services.Configure<MqttOptions>(
            configuration.GetSection("Mqtt"));

            services.AddScoped<IUserRepository, UserRepository>();

            services.AddScoped<IDeviceRepository, DeviceRepository>();

            services.AddScoped<
                IDoorbellCompositionRepository,
                DoorbellCompositionRepository>();

            services.AddScoped<
                IDoorbellRingEventRepository,
                DoorbellRingEventRepository>();

            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddHostedService<RingMqttConsumer>();
            return services;
        }
    }
}
