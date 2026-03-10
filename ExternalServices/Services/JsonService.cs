using System.Text.Encodings.Web;
using System.Text.Json;

namespace ExternalServices
{
    public static class JsonService
    {
        public static string Serialize(object obj)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = false,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            var json = JsonSerializer.Serialize(obj, options);
            return json;
        }
    }
}
