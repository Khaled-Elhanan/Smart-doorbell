using Doorbell.Application.Abstractions.Messaging;
using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Application.Abstractions.Repositories;
using Doorbell.Application.Abstractions.Storage;
using Doorbell.Infrastructure.BackgroundJobs;
using Doorbell.Infrastructure.Messaging.Mqtt;
using Doorbell.Infrastructure.Messaging.Mqtt.Consumers;
using Doorbell.Infrastructure.Messaging.Mqtt.Publishers;
using Doorbell.Infrastructure.Persistence.Repositories;
using Doorbell.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet;

namespace Doorbell.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<MqttOptions>(
                configuration.GetSection("Mqtt"));

            services.Configure<ObjectStorageOptions>(
                configuration.GetSection("ObjectStorage"));

            services.Configure<SnapshotReconcilerOptions>(
                configuration.GetSection("SnapshotReconciler"));

            services.AddScoped<IUserRepository, UserRepository>();

            services.AddScoped<IDeviceRepository, DeviceRepository>();

            services.AddScoped<
                IDoorbellCompositionRepository,
                DoorbellCompositionRepository>();

            services.AddScoped<
                IDoorbellRingEventRepository,
                DoorbellRingEventRepository>();

            services.AddScoped<IUnitOfWork, UnitOfWork>();

            services.AddScoped<
                IDeviceCommandRepository,
                DeviceCommandRepository>();

            services.AddSingleton<
                IStorageKeyGenerator,
                StorageKeyGenerator>();

            services.AddSingleton<LocalObjectStorage>();

            services.AddSingleton<IObjectStorage>(
                provider => provider.GetRequiredService<LocalObjectStorage>());

            services.AddSingleton<MqttClientFactory>();

            services.AddSingleton<MqttConnectionManager>();

            services.AddSingleton<
                IMqttCommandPublisher,
                MqttCommandPublisher>();

            services.AddHostedService<RingMqttConsumer>();

            services.AddHostedService<SnapshotResultConsumer>();

            services.AddHostedService<SnapshotTimeoutReconciler>();

            return services;
        }
    }
}