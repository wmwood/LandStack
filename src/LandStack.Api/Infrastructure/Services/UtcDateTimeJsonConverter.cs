using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LandStack.Api.Infrastructure.Services;

// Matches the previous REST DateTimeZoneHandling.Utc; SignalR keeps its own date contract.
public sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => AsUtc(reader.GetDateTime());

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(AsUtc(value));

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => value
    };
}
