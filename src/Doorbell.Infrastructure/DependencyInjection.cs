using Doorbell.Application.Abstractions.Messaging;
using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Application.Abstractions.Repositories;
using Doorbell.Application.Abstractions.Security;
using Doorbell.Application.Abstractions.Storage;
using Doorbell.Infrastructure.BackgroundJobs;
using Doorbell.Infrastructure.Messaging.Mqtt;
using Doorbell.Infrastructure.Messaging.Mqtt.Consumers;
using Doorbell.Infrastructure.Messaging.Mqtt.Publishers;
using Doorbell.Infrastructure.Messaging.Notifications;
using Doorbell.Infrastructure.Persistence.Repositories;
using Doorbell.Infrastructure.Security;
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
            services.AddOptions<MqttOptions>()
                .Bind(configuration.GetSection("Mqtt"))
                .Validate(
                    options => !string.IsNullOrWhiteSpace(options.Host)
                               && options.Port is > 0 and <= 65535
                               && options.SessionExpirySeconds >= 0,
                    "Invalid Mqtt configuration: Host is required, "
                    + "Port must be in the range 1-65535, "
                    + "SessionExpirySeconds must be >= 0.")
                .ValidateOnStart();

            services.Configure<ObjectStorageOptions>(
                configuration.GetSection("ObjectStorage"));

            services.Configure<DeviceCommandReconcilerOptions>(
                configuration.GetSection("DeviceCommandReconciler"));

            services.AddScoped<IUserRepository, UserRepository>();

            services.AddHttpContextAccessor();

            services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

            services.AddScoped<IDeviceRepository, DeviceRepository>();

            services.AddScoped<
                IDoorbellCompositionRepository,
                DoorbellCompositionRepository>();

            services.AddScoped<
                IDoorbellRingEventRepository,
                DoorbellRingEventRepository>();

            services.AddScoped<IUnitOfWork, UnitOfWork>();

            services.AddScoped<IOutboxRepository, OutboxRepository>();

            services.AddScoped<
                IUserDeviceTokenRepository,
                UserDeviceTokenRepository>();

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

            services.AddSingleton<MqttDeadLetterPublisher>();

            services.AddSingleton<
                IMqttCommandPublisher,
                MqttCommandPublisher>();

            services.AddHostedService<RingMqttConsumer>();

            services.AddHostedService<SnapshotResultConsumer>();

            services.AddHostedService<ChimeAckConsumer>();

            services.AddHostedService<DeviceCommandTimeoutReconciler>();

            services.Configure<NotificationOutboxOptions>(
                configuration.GetSection(NotificationOutboxOptions.SectionName));

            services.Configure<FirebaseMessagingOptions>(
                configuration.GetSection(FirebaseMessagingOptions.SectionName));

            services.AddSingleton<IUserConnectionRegistry, UserConnectionRegistry>();

            services.AddScoped<ISignalRNotificationSender, SignalRNotificationSender>();

            services.AddScoped<IFcmNotificationSender, FirebaseFcmSender>();

            services.AddScoped<INotificationDispatchService, NotificationDispatchService>();

            services.AddHostedService<NotificationOutboxProcessorJob>();

            return services;
        }
    }
}