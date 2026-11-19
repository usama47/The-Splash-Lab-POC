using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TheSplashLab.Infrastructure.Data;

/// <summary>
/// SQLite stores GUIDs as TEXT and compares case-sensitively.
/// Normalize to uppercase so reads/updates always match.
/// </summary>
public sealed class GuidUpperStringConverter : ValueConverter<Guid, string>
{
    public GuidUpperStringConverter()
        : base(
            guid => guid.ToString("D").ToUpperInvariant(),
            value => Guid.Parse(value))
    {
    }
}

public sealed class NullableGuidUpperStringConverter : ValueConverter<Guid?, string?>
{
    public NullableGuidUpperStringConverter()
        : base(
            guid => guid.HasValue ? guid.Value.ToString("D").ToUpperInvariant() : null,
            value => string.IsNullOrWhiteSpace(value) ? null : Guid.Parse(value))
    {
    }
}
