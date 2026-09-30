using Doorbell.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Infrastructure.Persistence.Configurations
{
    public sealed class AdvancedDoorbellCompositionConfiguration
    : IEntityTypeConfiguration<AdvancedDoorbellComposition>
    {
        public void Configure(
            EntityTypeBuilder<AdvancedDoorbellComposition> builder)
        {
            builder.ToTable("AdvancedDoorbellCompositions");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.RingButtonEntityCode)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.CameraEntityCode)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.ChimeEntityCode)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.DefaultChimeMelodyId)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.SnapshotTimeoutSeconds)
                .IsRequired();

            builder.HasOne<Device>()
                .WithOne()
                .HasForeignKey<AdvancedDoorbellComposition>(
                    x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => x.DeviceId)
                .IsUnique();
        }
    }
}
