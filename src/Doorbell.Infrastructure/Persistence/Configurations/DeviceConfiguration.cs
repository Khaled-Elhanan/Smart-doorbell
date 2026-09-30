using Doorbell.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Infrastructure.Persistence.Configurations
{
    public sealed class DeviceConfiguration
    : IEntityTypeConfiguration<Device>
    {
        public void Configure(EntityTypeBuilder<Device> builder)
        {
            builder.ToTable("Devices");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.DeviceType)
                .IsRequired()
                .HasMaxLength(100);

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
    }
