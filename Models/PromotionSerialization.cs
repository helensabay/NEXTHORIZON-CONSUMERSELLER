using System.Text.Json;

namespace MyAspNetApp.Models
{
    public static class PromotionSerialization
    {
        private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

        public static string Serialize(IEnumerable<string>? items)
        {
            return JsonSerializer.Serialize(items ?? new List<string>(), Options);
        }

        public static List<string> Deserialize(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new();

            try
            {
                return JsonSerializer.Deserialize<List<string>>(json, Options) ?? new();
            }
            catch
            {
                return new();
            }
        }
    }
}
