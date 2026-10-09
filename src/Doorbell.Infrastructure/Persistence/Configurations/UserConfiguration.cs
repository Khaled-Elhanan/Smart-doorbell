using Doorbell.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Doorbell.Infrastructure.Persistence.Configurations
{
    public sealed class UserConfiguration
    : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.ToTable("Users");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.SubjectId)
                .HasMaxLength(255);

            builder.Property(x => x.Email)
                .HasMaxLength(320);

            builder.Property(x => x.Username)
                .HasMaxLength(100);

            builder.HasIndex(x => x.SubjectId)
                .IsUnique();
        }
    }
}