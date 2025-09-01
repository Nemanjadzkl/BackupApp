using System.Text.Json;

namespace BackupApp.Helpers
{
    public static class JsonHelper
    {
        public static JsonSerializerOptions DefaultOptions => new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
}
