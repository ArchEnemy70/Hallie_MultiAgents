using System.Text.Json;

namespace Hallie.Services
{
    public sealed class RouterPlan
    {
        public List<ToolCall> Plan { get; set; } = new();
        public string Why { get; set; } = "";
    }

    public static class RouterPlanParser
    {
        public static RouterPlan Parse(string response)
        {
            var cleaned = CleanToFirstJsonObject(response);
            using var doc = JsonDocument.Parse(cleaned);
            var root = doc.RootElement;

            var plan = new RouterPlan();

            if (root.TryGetProperty("why", out var whyEl))
                plan.Why = whyEl.GetString() ?? "";

            if (root.TryGetProperty("plan", out var planEl) && planEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in planEl.EnumerateArray())
                {
                    var tool = item.GetProperty("tool").GetString() ?? "";
                    var parameters = new Dictionary<string, object>();

                    if (item.TryGetProperty("parameters", out var paramsEl) && paramsEl.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var p in paramsEl.EnumerateObject())
                            parameters[p.Name] = ExtractValue(p.Value);
                    }

                    plan.Plan.Add(new ToolCall { ToolName = tool, Parameters = parameters });
                }
            }

            return plan;
        }
        private static object ExtractValue(JsonElement el)
        {
            return el.ValueKind switch
            {
                JsonValueKind.String => el.GetString() ?? "",
                JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Object => el.ToString(),
                JsonValueKind.Array => el.ToString(),
                _ => ""
            };
        }

        // Prend le 1er objet JSON complet dans le texte (même si le modèle a bavé après)
        private static string CleanToFirstJsonObject(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException("Router: réponse vide");

            var s = text.Trim()
                .Replace("```json", "", StringComparison.OrdinalIgnoreCase)
                .Replace("```", "");

            int start = s.IndexOf('{');
            if (start < 0) throw new InvalidOperationException("Router: pas de JSON détecté");

            // scan pour trouver la fin de l'objet JSON (brace matching)
            int depth = 0;
            bool inString = false;
            char prev = '\0';

            for (int i = start; i < s.Length; i++)
            {
                var c = s[i];

                if (c == '"' && prev != '\\') inString = !inString;

                if (!inString)
                {
                    if (c == '{') depth++;
                    if (c == '}') depth--;

                    if (depth == 0)
                        return s.Substring(start, i - start + 1);
                }

                prev = c;
            }

            throw new InvalidOperationException("Router: JSON incomplet");
        }
    }

}
