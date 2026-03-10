using HallieDomain;

namespace Hallie.Services
{
    /// <summary>
    /// Service "feedback_log" interne : écrit en SQL (FeedbackLog) + indexe en vectoriel (Qdrant FeedbackRouting).
    ///
    /// Objectif : capturer le feedback explicite utilisateur (“bon/mauvais”) et le réutiliser pour router mieux.
    /// </summary>
    public sealed class FeedbackLogService
    {
        private readonly FeedbackDbService _db;
        private readonly FeedbackRoutingIndexService _indexVectoriel;

        public FeedbackLogService(FeedbackDbService db, FeedbackRoutingIndexService indexVectoriel)
        {
            _db = db;
            _indexVectoriel = indexVectoriel;
        }

        /// <summary>
        /// Log un feedback explicite utilisateur.
        /// toolCallsJson: JSON array des appels d'outils (nom + paramsJson). Si tu n'as pas le détail, passe "[]".
        /// toolUsed: pour l'indexation/analytics, on stocke aussi une version compacte (ex: "sql_query>create_bureatique_file>send_mail").
        /// </summary>
        public async Task<Guid> LogAsync(
            string conversationId,
            int turnId,
            int userRating,
            string outcome,
            string toolUsed,
            string toolCallsJson,
            string promptText,
            string? expectedTool = null,
            string? comment = null,
            string? errorClass = null,
            int? stepIndex = null,
            CancellationToken ct = default)
        {
            var id = await _db.InsertFeedbackAsync(new FeedbackLogInsertRequest
            {
                ConversationId = conversationId,
                TurnId = turnId,
                UserRating = userRating,
                Outcome = outcome,
                ErrorClass = errorClass,
                ToolUsed = toolUsed,
                ToolParamsJson = toolCallsJson,
                ExpectedTool = expectedTool,
                Comment = comment,
                PromptText = promptText,
                StepIndex = stepIndex,
            }, ct);

            if (id != Guid.Empty)
            {
                var payload = new
                {
                    feedback_id = id.ToString(),
                    tool_used = toolUsed,
                    rating = userRating,
                    outcome = outcome,
                    error_class = errorClass ?? ""
                };

                await _indexVectoriel.UpsertAsync(id, promptText, payload, ct);
            }
            return id;
        }

        public async Task<List<Guid>> LogWithDetailsAsync(
            string conversationId,
            int turnId,
            int chainUserRating,
            string chainOutcome,
            string chainToolUsed,
            string chainToolCallsJson,
            string promptText,
            //int? stepIndex = null,
            string? chainExpectedTool = null,
            string? chainComment = null,
            string? chainErrorClass = null,
            IEnumerable<ToolFeedbackEntry>? perToolEntries = null,
            CancellationToken ct = default)
        {
            var ids = new List<Guid>();

            ids.Add(await LogAsync(
                conversationId,
                turnId,
                chainUserRating,
                chainOutcome,
                chainToolUsed,
                chainToolCallsJson,
                promptText,
                chainExpectedTool,
                chainComment,
                chainErrorClass,
                null,
                ct));

            if (perToolEntries is null)
                return ids;
            int i = 0;
            foreach (var entry in perToolEntries.Where(e => !string.IsNullOrWhiteSpace(e.ToolName)))
            {
                ids.Add(await LogAsync(
                    conversationId,
                    turnId,
                    entry.UserRating,
                    entry.Outcome,
                    entry.ToolName,
                    string.IsNullOrWhiteSpace(entry.ToolParamsJson) ? "{}" : entry.ToolParamsJson,
                    promptText,
                    entry.ExpectedTool,
                    entry.Comment,
                    entry.ErrorClass,
                    i,
                    ct));
                i++;
            }

            return ids;
        }
    }

    public sealed class ToolFeedbackEntry
    {
        public string ToolName { get; set; } = "";
        public string ToolParamsJson { get; set; } = "{}";
        public int UserRating { get; set; }
        public string Outcome { get; set; } = "solved";
        public string? ErrorClass { get; set; }
        public string? ExpectedTool { get; set; }
        public string? Comment { get; set; }
        public int? StepIndex { get; set; }
    }
}
