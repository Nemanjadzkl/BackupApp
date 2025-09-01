using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BackupApp.Models
{
    public class JsonTimeSpanConverter : JsonConverter<TimeSpan>
    {
        public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if (string.IsNullOrEmpty(value))
                return TimeSpan.Zero;

            // Podržava različite formate
            if (TimeSpan.TryParse(value, out TimeSpan result))
                return result;

            // Pokušaj parsirati HH:mm format
            var parts = value.Split(':');
            if (parts.Length >= 2 && 
                int.TryParse(parts[0], out int hours) && 
                int.TryParse(parts[1], out int minutes))
            {
                if (hours >= 0 && hours < 24 && minutes >= 0 && minutes < 60)
                {
                    return new TimeSpan(hours, minutes, 0);
                }
            }

            throw new JsonException($"Nevažeći format vremena: '{value}'");
        }

        public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
        {
            writer.WriteStringValue($"{value.Hours:D2}:{value.Minutes:D2}");
        }
    }
}
