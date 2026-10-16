using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Doorbell.Infrastructure.Persistence.Converters;

public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(
            value => value.Kind == DateTimeKind.Utc
                ? value
                : value.ToUniversalTime(),
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
    {
    }
}
