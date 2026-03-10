using ExternalServices;
using Hallie.Tools;
using HallieDomain;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace HallieCore.Services
{
    public sealed record ApprovalRequest(
        string Id,
        string ToolName,
        string RiskLevel,        // low|medium|high
        string ActionSummary,    // texte lisible
        string PayloadJson,      // paramètres exacts (audit)
        DateTimeOffset CreatedAtUtc
    );
    public sealed record ApprovalDecision(
        string RequestId,
        bool Approved,
        string? Comment,
        DateTimeOffset DecidedAtUtc
    );
    public interface IApprovalService
    {
        Task<ApprovalDecision> RequestApprovalAsync(ApprovalRequest req, CancellationToken ct);
        void Resolve(string requestId, bool approved, string? comment = null);
        event Action<ApprovalRequest>? OnNewRequest; // pour UI
    }
    public sealed class ApprovalService : IApprovalService
    {
        private readonly ConcurrentDictionary<string, TaskCompletionSource<ApprovalDecision>> _pending = new();

        public event Action<ApprovalRequest>? OnNewRequest;

        public Task<ApprovalDecision> RequestApprovalAsync(ApprovalRequest req, CancellationToken ct)
        {
            LoggerService.LogDebug($"[APPROVAL] OnNewRequest fired id={req.Id}");

            var tcs = new TaskCompletionSource<ApprovalDecision>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (!_pending.TryAdd(req.Id, tcs))
                throw new InvalidOperationException($"Approval request already exists: {req.Id}");

            // Annulation : si l’utilisateur ferme l’app ou timeout
            ct.Register(() =>
            {
                if (_pending.TryRemove(req.Id, out var removed))
                    removed.TrySetCanceled(ct);
            });

            // Notifier l'UI
            OnNewRequest?.Invoke(req);

            return tcs.Task;
        }

        public void Resolve(string requestId, bool approved, string? comment = null)
        {
            if (_pending.TryRemove(requestId, out var tcs))
            {
                tcs.TrySetResult(new ApprovalDecision(
                    requestId,
                    approved,
                    comment,
                    DateTimeOffset.UtcNow
                ));
            }
        }
    }

    public enum PolicyDecisionType { Auto, RequireApproval, Deny }

    public sealed record PolicyDecision(PolicyDecisionType Type, string RiskLevel, string Reason);

    public static class ToolPolicy
    {
        public static PolicyDecision Evaluate(string toolName, bool isWhiteList)
        {
            // On regarde si exception / autorisation spécifique
            PolicyDecisionType decisionWhiteList = PolicyDecisionType.Auto;
            if (!isWhiteList)
                decisionWhiteList = PolicyDecisionType.RequireApproval;

            // Ajuster selon les besoins
            return toolName switch
            {
                "create_bureatique_file" => new PolicyDecision(PolicyDecisionType.Auto, "low", "Create a new document"),
                "get_weather" => new PolicyDecision(PolicyDecisionType.Auto, "low", "Just open browser"),
                "pictures_video_analyse" => new PolicyDecision(PolicyDecisionType.Auto, "low", "Read-only"),
                "road_route" => new PolicyDecision(PolicyDecisionType.Auto, "low", "Read-only"),
                "audio_video_transcrib" => new PolicyDecision(PolicyDecisionType.Auto, "low", "Read-only"),
                "web_search" => new PolicyDecision(PolicyDecisionType.Auto, "low", "Read-only"),
                "web_fetch" => new PolicyDecision(PolicyDecisionType.Auto, "low", "Read-only"),
                "calendar_search" => new PolicyDecision(PolicyDecisionType.Auto, "low", "Read-only"),
                "search_documents" => new PolicyDecision(PolicyDecisionType.Auto, "low", "Read-only"),
                "IngestDocumentTool" => new PolicyDecision(PolicyDecisionType.Auto, "low", "Read-only"),
                "sql_query" => new PolicyDecision(PolicyDecisionType.Auto, "medium", "Read-only"),
                "extract_file" => new PolicyDecision(PolicyDecisionType.Auto, "medium", "Read-only"),
                "open_file" => new PolicyDecision(PolicyDecisionType.Auto, "medium", "Read-only"),
                "liste_files" => new PolicyDecision(PolicyDecisionType.Auto, "medium", "Read-only"),
                "find_file" => new PolicyDecision(PolicyDecisionType.Auto, "medium", "Read-only"),
                "suivi_mails" => new PolicyDecision(PolicyDecisionType.Auto, "medium", "Read-only"),
                "press_review" => new PolicyDecision(PolicyDecisionType.Auto, "medium", "Read-only"),

                "commands_windows" => new PolicyDecision(decisionWhiteList, "high", "Executes OS command"),

                "extract_zip" => new PolicyDecision(PolicyDecisionType.RequireApproval, "medium", "maybe danger inside zip file"),
                "copy_file" => new PolicyDecision(PolicyDecisionType.RequireApproval, "medium", "create new file"),
                "sql_action" => new PolicyDecision(PolicyDecisionType.RequireApproval, "high", "Write into database"),
                "calendar_create" => new PolicyDecision(PolicyDecisionType.RequireApproval, "medium", "Creates calendar event"),
                "calendar_delete" => new PolicyDecision(PolicyDecisionType.RequireApproval, "high", "Deletes calendar events"),
                "send_mail" => new PolicyDecision(PolicyDecisionType.RequireApproval, "high", "Sends external communication"),
                "delete_file" => new PolicyDecision(PolicyDecisionType.RequireApproval, "high", "delete a file"),
                "rename_file" => new PolicyDecision(PolicyDecisionType.RequireApproval, "high", "rename a file"),

                _ => new PolicyDecision(PolicyDecisionType.RequireApproval, "high", "Unknown tool => safer to approve")
            };
        }
    }

    public sealed record ToolCallApprob(string ToolName, Dictionary<string, object> Parameters, string ParametersJson);

    public interface IToolExecutor
    {
        Task<string> ExecuteAsync(ToolCallApprob call, CancellationToken ct);
    }

    public sealed class SafeToolRunner
    {
        private readonly ITool _executor;
        private readonly IApprovalService _approval;
        private readonly IApprovalSummaryBuilder _summary;
        public SafeToolRunner(ITool executor, IApprovalService approval, IApprovalSummaryBuilder summary)
        {
            _executor = executor;
            _approval = approval;
            _summary = summary;
        }

        public async Task<string> RunAsync(ToolCallApprob call, CancellationToken ct=default!)
        {
            // On regarde s'il y a des exceptions
            var isWhiteList = false;
            if (call.ToolName == "commands_windows")
            {
                var Wl = Params.CommandesWindows_WhiteList!.Split(",").ToList();

                var param1 = call.Parameters.ContainsKey("commande")
                    ? call.Parameters["commande"].ToString()
                    : null;

                if(param1 != null)
                {
                    isWhiteList = Wl.Contains(param1);
                }
            }

            var policy = ToolPolicy.Evaluate(call.ToolName, isWhiteList);

            if (policy.Type == PolicyDecisionType.Deny)
                throw new InvalidOperationException($"Tool denied: {call.ToolName}. Reason: {policy.Reason}");

            if (policy.Type == PolicyDecisionType.RequireApproval)
            {
                var req = new ApprovalRequest(
                    Id: Guid.NewGuid().ToString("N"),
                    ToolName: call.ToolName,
                    RiskLevel: policy.RiskLevel,
                    ActionSummary: _summary.BuildSummary(call.ToolName, call.ParametersJson),
                    PayloadJson: call.ParametersJson,
                    CreatedAtUtc: DateTimeOffset.UtcNow
                );

                LoggerService.LogDebug($"[APPROVAL] Requesting approval for tool={call.ToolName}");
                var decision = await _approval.RequestApprovalAsync(req, ct);

                if (!decision.Approved)
                    return $"Action refused by user. Tool={call.ToolName}. Comment={decision.Comment ?? ""}";

                // validé => on exécute
                return await _executor.ExecuteAsync(call.Parameters);
            }

            // Auto => execute
            return await _executor.ExecuteAsync(call.Parameters);
        }
    }





    public interface IApprovalSummaryBuilder
    {
        string BuildSummary(string toolName, string payloadJson);
    }

    public sealed class ApprovalSummaryBuilder : IApprovalSummaryBuilder
    {
        public string BuildSummary(string toolName, string payloadJson)
        {
            return toolName switch
            {
                "send_mail" => BuildSendMail(payloadJson),
                "calendar_delete" => BuildCalendarDelete(payloadJson),
                "calendar_create" => BuildCalendarCreate(payloadJson),
                "sql_action" => BuildSqlAction(payloadJson),
                "delete_file" => BuildDeleteFile(payloadJson),
                "rename_file" => BuildRenameFile(payloadJson),
                "commands_windows" => BuildCommandsWindows(payloadJson),
                "copy_file" => BuildCopyFile(payloadJson),
                "extract_zip" => BuildExtractZip(payloadJson),
                _ => BuildGeneric(toolName, payloadJson)
            };
        }
        private static string BuildSendMail(string payloadJson)
        {
            var (subject, to, content, attachments) = ReadSendMail(payloadJson);

            var sb = new StringBuilder();
            sb.AppendLine("📧 Envoi d’un e-mail");
            sb.AppendLine($"• Destinataires : {to}");
            sb.AppendLine($"• Sujet : {subject}");
            if (!string.IsNullOrWhiteSpace(attachments))
                sb.AppendLine($"• Pièces jointes : {attachments}");
            sb.AppendLine();
            sb.AppendLine("Aperçu du message :");
            sb.AppendLine(Preview(content, 400));
            return sb.ToString();
        }
        private static string BuildSqlAction(string payloadJson)
        {
            var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            var query = root.TryGetProperty("query", out var q) ? q.GetString() : null;
            var bddname = root.TryGetProperty("bddname", out var a) ? a.GetString() : null;

            var sb = new StringBuilder();
            sb.AppendLine("☠️ Action sur une table de base de données");
            sb.AppendLine($"• Base de données : {bddname}");
            sb.AppendLine($"");
            sb.AppendLine($"• Requête :\n{query}");
            return sb.ToString();
        }
        private static string BuildDeleteFile(string payloadJson)
        {
            var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            var fullfilename = root.TryGetProperty("fullfilename", out var q) ? q.GetString() : null;

            var sb = new StringBuilder();
            sb.AppendLine("☠️ Suppression d'un fichier");
            sb.AppendLine($"• Fichier : {fullfilename}");
            sb.AppendLine($"");
            return sb.ToString();
        }
        private static string BuildRenameFile(string payloadJson)
        {
            var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            var fullfilenameOld = root.TryGetProperty("fullfilenameOld", out var q1) ? q1.GetString() : null;
            var fullfilenameNew = root.TryGetProperty("fullfilenameNew", out var q2) ? q2.GetString() : null;

            var sb = new StringBuilder();
            sb.AppendLine("☠️ Renommage d'un fichier");
            sb.AppendLine($"• Ancien nom : {fullfilenameOld}");
            sb.AppendLine($"• Nouveau nom : {fullfilenameNew}");
            sb.AppendLine($"");
            return sb.ToString();
        }
        private static string BuildCommandsWindows(string payloadJson)
        {
            var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            var commande = root.TryGetProperty("commande", out var q1) ? q1.GetString() : null;

            var sb = new StringBuilder();
            sb.AppendLine("☠️ Commande Windows");
            sb.AppendLine($"• Commande demandée : {commande}");
            sb.AppendLine($"");
            return sb.ToString();
        }
        private static string BuildCopyFile(string payloadJson)
        {
            var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            var fullfilenameOrigine = root.TryGetProperty("fullfilenameOrigine", out var q1) ? q1.GetString() : null;
            var fullfilenameDestination = root.TryGetProperty("fullfilenameDestination", out var q2) ? q2.GetString() : null;

            var sb = new StringBuilder();
            sb.AppendLine("☠️ Copie de fichier");
            sb.AppendLine($"• Origine : {fullfilenameOrigine}");
            sb.AppendLine($"• Destination : {fullfilenameDestination}");
            sb.AppendLine($"");
            return sb.ToString();
        }
        private static string BuildExtractZip(string payloadJson)
        {
            var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            var zipfilename = root.TryGetProperty("zipfilename", out var q1) ? q1.GetString() : null;
            var path = root.TryGetProperty("zipfilename", out var q4) ? q4.GetString() : null;
            var isSummaryFiles = root.TryGetProperty("isSummaryFiles", out var q2) ? q2.GetString() : null;
            var isDeleteDirectory = root.TryGetProperty("isDeleteDirectory", out var q3) ? q3.GetString() : null;

            isSummaryFiles = isSummaryFiles == "1" ? "Oui" : "Non";
            isDeleteDirectory = isDeleteDirectory == "1" ? "Oui" : "Non";

            var sb = new StringBuilder();
            sb.AppendLine("☠️ Décompression d'un fichier ZIP");
            sb.AppendLine($"• Fichier ZIP : {zipfilename}");
            if(path != null && path != "")
                sb.AppendLine($"• Path décompression : {path}");
            sb.AppendLine($"• Faire un résumé du contenu ? --> {isSummaryFiles}");
            sb.AppendLine($"• Supprimer le répertoire après extraction ? --> {isDeleteDirectory}");
            sb.AppendLine($"");
            return sb.ToString();
        }
        //
        private static string BuildCalendarDelete(string payloadJson)
        {
            var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            var query = root.TryGetProperty("query", out var q) ? q.GetString() : null;
            var min = root.TryGetProperty("timemin", out var a) ? a.GetString() : null;
            var max = root.TryGetProperty("timemax", out var b) ? b.GetString() : null;

            var sb = new StringBuilder();
            sb.AppendLine("🗑️ Suppression d’événements calendrier");
            if (!string.IsNullOrWhiteSpace(query)) sb.AppendLine($"• Filtre : {query}");
            if (!string.IsNullOrWhiteSpace(min) || !string.IsNullOrWhiteSpace(max))
                sb.AppendLine($"• Période : {min ?? "?"} → {max ?? "?"}");
            sb.AppendLine();
            sb.AppendLine("⚠️ Action potentiellement irréversible.");
            return sb.ToString();
        }
        private static string BuildCalendarCreate(string payloadJson)
        {
            var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            string title = root.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
            string start = root.TryGetProperty("start", out var s) ? s.GetString() ?? "" : "";
            string end = root.TryGetProperty("end", out var e) ? e.GetString() ?? "" : "";
            string location = root.TryGetProperty("location", out var l) ? l.GetString() ?? "" : "";
            string rrule = root.TryGetProperty("rrule", out var r) ? r.GetString() ?? "" : "";
            string attendees = root.TryGetProperty("attendeesEmails", out var at) ? at.GetString() ?? "" : "";

            var sb = new StringBuilder();
            sb.AppendLine("📅 Création d’un événement calendrier");
            sb.AppendLine($"• Titre : {title}");
            sb.AppendLine($"• Début : {start}");
            sb.AppendLine($"• Fin : {end}");
            if (!string.IsNullOrWhiteSpace(location)) sb.AppendLine($"• Lieu : {location}");
            if (!string.IsNullOrWhiteSpace(attendees)) sb.AppendLine($"• Invités : {attendees}");
            if (!string.IsNullOrWhiteSpace(rrule)) sb.AppendLine($"• Récurrence : {rrule}");
            return sb.ToString();
        }

        private static string BuildGeneric(string toolName, string payloadJson)
            => $"Action : {toolName}\n\nDétails :\n{payloadJson}";

        private static (string subject, string to, string content, string attachments) ReadSendMail(string payloadJson)
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            string subject = root.TryGetProperty("subject", out var s) ? s.GetString() ?? "(sans sujet)" : "(sans sujet)";
            string to = root.TryGetProperty("destinataires", out var d) ? d.GetString() ?? "(vide)" : "(vide)";
            string content = root.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
            string attachments = root.TryGetProperty("attachments", out var a) ? a.GetString() ?? "" : "";
            return (subject, to, content, attachments);
        }

        private static string Preview(string text, int maxLen)
        {
            if (string.IsNullOrWhiteSpace(text)) return "(vide)";
            text = text.Trim();
            return text.Length <= maxLen ? text : text.Substring(0, maxLen) + "…";
        }
    }
}
