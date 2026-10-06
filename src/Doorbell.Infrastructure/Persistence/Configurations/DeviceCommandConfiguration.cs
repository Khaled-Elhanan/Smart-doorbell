using Doorbell.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Doorbell.Infrastructure.Persistence.Configurations;

public sealed class DeviceCommandConfiguration
    : IEntityTypeConfiguration<DeviceCommand>
{
    public void Configure(EntityTypeBuilder<DeviceCommand> builder)
    {
        builder.ToTable("DeviceCommands");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EntityCode)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.RequestId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.CommandType)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.Attempt)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(1000);

        builder.Property(x => x.StorageKey)
            .HasMaxLength(500);

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasIndex(x => x.RequestId)
            .IsUnique();

        builder.HasIndex(x => new { x.RingEventId, x.CommandType })
            .IsUnique();

        builder.HasIndex(x => x.StorageKey);

        builder.HasOne<DoorbellRingEvent>()
            .WithMany()
            .HasForeignKey(x => x.RingEventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}