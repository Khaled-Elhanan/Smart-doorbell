using Doorbell.Domain.Entities;
using Doorbell.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;

namespace Doorbell.Infrastructure.Persistence.Context
{
    public sealed class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();

        public DbSet<Device> Devices => Set<Device>();

        public DbSet<AdvancedDoorbellComposition>
            AdvancedDoorbellCompositions => Set<AdvancedDoorbellComposition>();

        public DbSet<DoorbellRingEvent> DoorbellRingEvents =>
            Set<DoorbellRingEvent>();
        public DbSet<DeviceCommand> DeviceCommands { get; set; }

        public DbSet<NotificationOutbox> NotificationOutboxes =>
            Set<NotificationOutbox>();

        public DbSet<NotificationAttempt> NotificationAttempts =>
            Set<NotificationAttempt>();

        protected override void ConfigureConventions(
            ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder
                .Properties<DateTime>()
                .HaveConversion<UtcDateTimeConverter>();

            base.ConfigureConventions(configurationBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(ApplicationDbContext).Assembly);

            base.OnModelCreating(modelBuilder);
        }
    }
}
