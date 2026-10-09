using Doorbell.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Doorbell.Infrastructure.Persistence.Configurations
{
    public sealed class NotificationAttemptConfiguration
        : IEntityTypeConfiguration<NotificationAttempt>
    {
        public void Configure(EntityTypeBuilder<NotificationAttempt> builder)
        {
            builder.ToTable("NotificationAttempts");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.OutboxId)
                .IsRequired();

            builder.Property(x => x.AttemptNumber)
                .IsRequired();

            builder.Property(x => x.AttemptedAtUtc)
                .IsRequired();

            builder.Property(x => x.Succeeded)
                .IsRequired();

            builder.Property(x => x.ErrorMessage)
                .HasMaxLength(1000);

            builder.HasIndex(x => new { x.OutboxId, x.AttemptNumber })
                .IsUnique();

            builder.HasOne<NotificationOutbox>()
                .WithMany()
                .HasForeignKey(x => x.OutboxId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}