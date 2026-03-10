using HallieDomain;
using System.Text;
using System.Text.Json;

namespace Hallie.Services
{
    /// <summary>
    /// Few-shot dynamique injecté dans le router LLM.
    ///
    /// Pipeline :
    /// 1) Similarité vectorielle sur prompts passés (Qdrant collection FeedbackRouting)
    /// 2) Lecture des détails dans SQL Server (dbo.FeedbackLog)
    /// 3) Ajout de règles (dbo.ToolRoutingRules) sous forme d'indices.
    ///
    /// pour "prompts similaires" : embedding (nomic-embed-text) + Qdrant topK + seuil.
    /// </summary>
    public sealed class RouterFewShotService
    {
        private readonly FeedbackDbService _db;
        private readonly FeedbackRoutingIndexService _indexVectoriel;
        private readonly ToolRoutingRuleEngine _rules;

        public RouterFewShotService(FeedbackDbService db, FeedbackRoutingIndexService indexVectoriel)
        {
            _db = db;
            _indexVectoriel = indexVectoriel;
            _rules = new ToolRoutingRuleEngine();
        }

        public async Task<string> BuildRouterContextAsync(string userPrompt, CancellationToken ct = default)
        {
            // 1) Similar feedbacks
            var hits = await _indexVectoriel.SearchSimilarAsync(userPrompt, topK: 12, minScore: 0.55f, ct: ct);
            var ids = hits.Select(h => h.Id).ToList();
            var rows = await _db.GetFeedbackByIdsAsync(ids, ct);

            // garde-fous : on veut de la diversité + éviter trop vieux / trop verbeux
            var ordered = rows
                .OrderByDescending(r => r.CreatedAtUtc)
                .Take(20)
                .ToList();

            var good = ordered
                .Where(r => r.UserRating >= 4 && r.Outcome.Equals("solved", StringComparison.OrdinalIgnoreCase))
                .Take(4)
                .ToList();

            var bad = ordered
                .Where(r => r.UserRating <= 2 || r.Outcome.Equals("failed", StringComparison.OrdinalIgnoreCase))
                .Take(3)
                .ToList();

            // 2) ToolRoutingRules => indices scorés
            var enabledRules = await _db.GetEnabledRulesAsync(ct);
            var ruleMatches = _rules.Evaluate(userPrompt, enabledRules)
                .OrderByDescending(m => m.ScoreDelta)
                .Take(8)
                .ToList();

            if (good.Count == 0 && bad.Count == 0 && ruleMatches.Count == 0)
                return "";

            var sb = new StringBuilder();
            sb.AppendLine("# CONTEXTE DE ROUTAGE (FEEDBACK + REGLES)\n");
            sb.AppendLine("Tu as le droit de t'en servir comme indices, mais tu DOIS toujours suivre les consignes du prompt system et retourner un JSON strict.\n");

            if (ruleMatches.Count > 0)
            {
                sb.AppendLine("## Règles (indices)");
                foreach (var rm in ruleMatches)
                    sb.AppendLine($"- {rm.Tool}: score {rm.ScoreDelta} (règle: {rm.RuleName}, confiance: {rm.Confidence})");
                sb.AppendLine();
            }

            if (good.Count > 0)
            {
                sb.AppendLine("## Exemples (bons choix d'outils)");
                foreach (var r in good)
                    AppendExample(sb, r);
                sb.AppendLine();
            }

            if (bad.Count > 0)
            {
                sb.AppendLine("## Exemples (mauvais choix d'outils à éviter)");
                foreach (var r in bad)
                    AppendExample(sb, r);
                sb.AppendLine();
            }

            sb.AppendLine("# FIN CONTEXTE\n");
            return sb.ToString();
        }

        private static void AppendExample(StringBuilder sb, FeedbackLogRow r)
        {
            // On coupe pour éviter d'injecter des romans dans le router
            static string Clip(string s, int max)
            {
                if (string.IsNullOrWhiteSpace(s)) return "";
                s = s.Replace("\r", " ").Replace("\n", " ").Trim();
                return s.Length <= max ? s : s.Substring(0, max) + "…";
            }

            sb.AppendLine($"- Prompt: {Clip(r.PromptText, 240)}");
            sb.AppendLine($"  Outil utilisé: {r.ToolUsed} (rating={r.UserRating}, outcome={r.Outcome}, error={r.ErrorClass ?? ""})");

            if (!string.IsNullOrWhiteSpace(r.ExpectedTool))
                sb.AppendLine($"  Outil attendu: {r.ExpectedTool}");

            if (!string.IsNullOrWhiteSpace(r.Comment))
                sb.AppendLine($"  Commentaire: {Clip(r.Comment!, 240)}");

            // params hash + aperçu
            var paramsPreview = Clip(r.ToolParamsJson, 240);
            sb.AppendLine($"  Params: {paramsPreview}");
        }
    }
}
