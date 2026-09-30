using Doorbell.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Infrastructure.Persistence.Configurations
{
    public sealed class DoorbellRingEventConfiguration
    : IEntityTypeConfiguration<DoorbellRingEvent>
    {
        public void Configure(
            EntityTypeBuilder<DoorbellRingEvent> builder)
        {
            builder.ToTable("DoorbellRingEvents");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.DeviceEventId)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.RingTimestampUtc)
                .IsRequired();

            builder.Property(x => x.CreatedAtUtc)
                .IsRequired();

            builder.Property(x => x.SnapshotStatus)
                .IsRequired();

            builder.Property(x => x.SnapshotStorageKey)
                .HasMaxLength(500);

            builder.Property(x => x.SnapshotResolution)
                .HasMaxLength(50);

            builder.Property(x => x.ChimeStatus)
                .IsRequired();

            builder.Property(x => x.ChimeMelodyId)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.AcknowledgeStatus)
                .IsRequired();

            builder.HasOne<Device>()
                .WithMany()
                .HasForeignKey(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => new
            {
                x.DeviceId,
                x.DeviceEventId
            })
            .IsUnique();
        }
    }
}
