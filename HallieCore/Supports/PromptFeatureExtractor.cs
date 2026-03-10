using ExternalServices;

namespace Hallie.Services
{
    public static class PromptFeatureExtractor
    {
        public static Dictionary<string, bool> Extract(string prompt)
        {
            var p = prompt.ToLowerInvariant();

            return new Dictionary<string, bool>
            {
                ["mentions_sql"] = p.Contains("sql") || p.Contains("base") || p.Contains("requête"),
                ["mentions_email"] = p.Contains("mail") || p.Contains("email"),
                ["needs_document"] = p.Contains("document") || p.Contains("rapport") || p.Contains("word"),
                ["has_url"] = p.Contains("http://") || p.Contains("https://"),
                ["mentions_weather"] = p.Contains("météo"),
                ["mentions_calendar"] = p.Contains("agenda") || p.Contains("calendrier")
            };
        }
    }

    public class ToolScoringService
    {
        public Dictionary<string, int> ScoreTools(string prompt)
        {
            var features = PromptFeatureExtractor.Extract(prompt);

            var scores = new Dictionary<string, int>();

            if (features["mentions_sql"])
                scores["sql_query"] = 8;

            if (features["needs_document"])
                scores["create_bureatique_file"] = 7;

            if (features["mentions_email"])
                scores["send_mail"] = 6;

            if (features["has_url"])
                scores["web_fetch"] = 5;

            if (features["mentions_weather"])
                scores["get_weather"] = 6;

            return scores;
        }
    }


    public static class PlanValidator
    {
        public static bool ValidatePlan(List<ToolCall> tools)
        {
            if (tools == null || tools.Count == 0)
                return false;

            foreach (var t in tools)
            {
                if (string.IsNullOrWhiteSpace(t.ToolName))
                    return false;
            }

            return true;
        }
    }
    public static class ToolExecutionLogger
    {
        public static void LogStep(int stepIndex, string tool, string parameters, bool success, long duration)
        {
            LoggerService.LogDebug(
                $"TOOL STEP {stepIndex} | {tool} | success={success} | duration={duration}ms | params={parameters}"
            );
        }
    }
}



