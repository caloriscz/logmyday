using System.Text.Json;
using System.Text.Json.Serialization;

namespace LogMyDay.Shared.Serialization;

public static class JsonSerializationSettings
{
    public static JsonSerializerOptions CreateDefault()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Configure(options);
        return options;
    }

    public static void Configure(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Converters.Any(static c => c is JsonStringEnumConverter))
        {
            options.Converters.Add(new JsonStringEnumConverter());
        }

        if (!options.Converters.Any(static c => c is FlexibleTimeSpanConverter))
        {
            options.Converters.Add(new FlexibleTimeSpanConverter());
        }
    }
}
