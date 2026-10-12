using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MinGo.StaticPages.Infrastructure.Data;

/// <summary>
/// Stores DateTimeOffset as UTC Unix milliseconds (long) in the database.
/// </summary>
public static class DateTimeConverters
{
    public static readonly ValueConverter<DateTimeOffset, long> ToUnixMs = new(
        v => v.ToUniversalTime().ToUnixTimeMilliseconds(),
        v => DateTimeOffset.FromUnixTimeMilliseconds(v));

    public static readonly ValueConverter<DateTimeOffset?, long?> ToUnixMsNullable = new(
        v => v.HasValue ? v.Value.ToUniversalTime().ToUnixTimeMilliseconds() : null,
        v => v.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(v.Value) : null);

    /// <summary>Parse a stored value into UTC Unix milliseconds (handles legacy TEXT dates).</summary>
    public static long? ParseToUnixMs(object? value)
    {
        switch (value)
        {
            case null or DBNull:
                return null;
            case long l:
                return l;
            case int i:
                return i;
            case double d:
                return (long)d;
            case string s when long.TryParse(s, out var asLong):
                return asLong;
            case string s when DateTimeOffset.TryParse(s, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dto):
                return dto.ToUniversalTime().ToUnixTimeMilliseconds();
            case DateTimeOffset dto:
                return dto.ToUniversalTime().ToUnixTimeMilliseconds();
            case DateTime dt:
                return new DateTimeOffset(dt.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                    : dt.ToUniversalTime()).ToUnixTimeMilliseconds();
            default:
                return null;
        }
    }
}
