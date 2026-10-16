using Doorbell.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Doorbell.Infrastructure.Persistence.Configurations
{
    public sealed class NotificationOutboxConfiguration
        : IEntityTypeConfiguration<NotificationOutbox>
    {
        public void Configure(EntityTypeBuilder<NotificationOutbox> builder)
        {
            builder.ToTable("NotificationOutboxes");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.RingEventId)
                .IsRequired();

            builder.Property(x => x.Kind)
                .IsRequired();

            builder.Property(x => x.RecipientUserId)
                .IsRequired();

            builder.Property(x => x.Status)
                .IsRequired();

            builder.Property(x => x.AttemptCount)
                .IsRequired();

            builder.Property(x => x.CreatedAtUtc)
                .IsRequired();

            builder.Property(x => x.RowVersion)
                .IsRowVersion();

            builder.HasIndex(x => new { x.RingEventId, x.Kind })
                .IsUnique();

            builder.HasOne<DoorbellRingEvent>()
                .WithMany()
                .HasForeignKey(x => x.RingEventId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.RecipientUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}