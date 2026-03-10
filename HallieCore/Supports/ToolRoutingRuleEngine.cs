using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hallie.Services
{
    public sealed class ToolRoutingRuleEngine
    {
        public sealed record RuleMatch(string Tool, int ScoreDelta, string RuleName, decimal Confidence);

        /// <summary>
        /// Version 1 (mieux) : règles simples et rapides (pas de NLP exotique).
        /// ConditionJson supporté :
        /// {
        ///   "containsAny": ["mot", "phrase"],
        ///   "containsAll": ["mot", ...],
        ///   "regex": "...",
        ///   "minLen": 20
        /// }
        /// Tous les champs sont optionnels. Si plusieurs champs présents, ils doivent tous matcher.
        /// </summary>
        public List<RuleMatch> Evaluate(string userPrompt, IEnumerable<ToolRoutingRuleRow> rules)
        {
            var text = userPrompt ?? "";
            var norm = text.ToLowerInvariant();

            var matches = new List<RuleMatch>();
            foreach (var r in rules)
            {
                if (!r.IsEnabled) continue;
                if (string.IsNullOrWhiteSpace(r.ConditionJson)) continue;

                if (!TryParseCondition(r.ConditionJson, out var cond))
                    continue;

                if (!ConditionMatches(norm, text, cond))
                    continue;

                matches.Add(new RuleMatch(r.Tool, r.ScoreDelta, r.Name, r.Confidence));
            }

            return matches;
        }

        private static bool TryParseCondition(string json, out JsonElement root)
        {
            root = default;
            try
            {
                using var doc = JsonDocument.Parse(json);
                root = doc.RootElement.Clone();
                return root.ValueKind == JsonValueKind.Object;
            }
            catch
            {
                return false;
            }
        }

        private static bool ConditionMatches(string normLower, string original, JsonElement cond)
        {
            // minLen
            if (cond.TryGetProperty("minLen", out var minLenEl) && minLenEl.ValueKind == JsonValueKind.Number)
            {
                var minLen = minLenEl.GetInt32();
                if (original.Length < minLen) return false;
            }

            // containsAny
            if (cond.TryGetProperty("containsAny", out var anyEl) && anyEl.ValueKind == JsonValueKind.Array)
            {
                var ok = false;
                foreach (var item in anyEl.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String) continue;
                    var s = item.GetString()?.ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(s)) continue;
                    if (normLower.Contains(s)) { ok = true; break; }
                }
                if (!ok) return false;
            }

            // containsAll
            if (cond.TryGetProperty("containsAll", out var allEl) && allEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in allEl.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String) continue;
                    var s = item.GetString()?.ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(s)) continue;
                    if (!normLower.Contains(s)) return false;
                }
            }

            // regex
            if (cond.TryGetProperty("regex", out var reEl) && reEl.ValueKind == JsonValueKind.String)
            {
                var pattern = reEl.GetString();
                if (!string.IsNullOrWhiteSpace(pattern))
                {
                    if (!Regex.IsMatch(original, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                        return false;
                }
            }

            return true;
        }
    }
}
