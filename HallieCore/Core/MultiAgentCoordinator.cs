using ExternalServices;
using Hallie.Tools;
using HallieDomain;
using System.Text;
using System.Text.Json;
using HallieCore.Services;

namespace Hallie.Services
{
    public sealed class MultiAgentCoordinator
    {
        private readonly ToolRegistry _toolRegistry;
        private readonly IApprovalService _approvalService;
        private readonly IApprovalSummaryBuilder _approvalSummary;
        private readonly Func<string, string, List<ConversationMessage>, Task<RouterPlan>> _planner;
        private readonly Func<string, string, Dictionary<string, object>?, List<ConversationMessage>, Task<ToolCall>> _specialist;
        private readonly Func<string, ToolCall, string, List<ConversationMessage>, Task<CriticDecision>> _critic;
        private readonly Func<SupervisorInput, List<ConversationMessage>, Task<SupervisorDecision>> _supervisor;
        private readonly Func<string, List<(string ToolName, string ResultJson)>, Task<string>> _synthesizer;
        private readonly Func<string, Task<string>> _directResponder;
        private readonly AgentMemoryStore _memoryStore;
        private readonly Action<string>? _toolSelected;
        private readonly Action<MultiAgentTrace>? _trace;
        private readonly Action<string, string>? _toolCallTrace;
        private readonly MultiAgentBudget _budget;

        public MultiAgentCoordinator(
            ToolRegistry toolRegistry,
            IApprovalService approvalService,
            IApprovalSummaryBuilder approvalSummary,
            Func<string, string, List<ConversationMessage>, Task<RouterPlan>> planner,
            Func<string, string, Dictionary<string, object>?, List<ConversationMessage>, Task<ToolCall>> specialist,
            Func<string, ToolCall, string, List<ConversationMessage>, Task<CriticDecision>> critic,
            Func<SupervisorInput, List<ConversationMessage>, Task<SupervisorDecision>> supervisor,
            Func<string, List<(string ToolName, string ResultJson)>, Task<string>> synthesizer,
            Func<string, Task<string>> directResponder,
            AgentMemoryStore memoryStore,
            MultiAgentBudget? budget = null,
            Action<string>? toolSelected = null,
            Action<MultiAgentTrace>? trace = null,
            Action<string, string>? toolCallTrace = null)
        {
            _toolRegistry = toolRegistry;
            _approvalService = approvalService;
            _approvalSummary = approvalSummary;
            _planner = planner;
            _specialist = specialist;
            _critic = critic;
            _supervisor = supervisor;
            _synthesizer = synthesizer;
            _directResponder = directResponder;
            _memoryStore = memoryStore;
            _budget = budget ?? new MultiAgentBudget();
            _toolSelected = toolSelected;
            _trace = trace;
            _toolCallTrace = toolCallTrace;
        }

        private static SupervisorDecision? EvaluateSupervisorDeterministically(SupervisorInput input)
        {
            if (input.Phase == "pre_execute")
            {
                if (string.IsNullOrWhiteSpace(input.ToolName))
                {
                    return new SupervisorDecision
                    {
                        Decision = "stop",
                        Why = "Aucun outil courant.",
                        TechnicalSuccess = false,
                        TaskSuccess = false
                    };
                }

                if (input.Parameters == null)
                {
                    return new SupervisorDecision
                    {
                        Decision = "replan",
                        Why = "Paramètres absents.",
                        TechnicalSuccess = false,
                        TaskSuccess = false
                    };
                }

                var tool = input.ToolName.Trim().ToLowerInvariant();

                // -------- SQL / recherche : toujours exécutable si paramètres minimaux présents
                if (tool == "sql_query")
                {
                    var hasDb = input.Parameters.TryGetValue("bddname", out var dbObj) &&
                                !string.IsNullOrWhiteSpace(dbObj?.ToString());

                    var hasQuery = input.Parameters.TryGetValue("query", out var queryObj) &&
                                   !string.IsNullOrWhiteSpace(queryObj?.ToString());

                    return new SupervisorDecision
                    {
                        Decision = (hasDb && hasQuery) ? "continue" : "replan",
                        Why = (hasDb && hasQuery)
                            ? "Pré-contrôle valide pour sql_query."
                            : "Paramètres sql_query incomplets.",
                        TechnicalSuccess = hasDb && hasQuery,
                        TaskSuccess = hasDb && hasQuery
                    };
                }

                if (tool == "search_documents" || tool == "web_search" || tool == "web_fetch")
                {
                    return new SupervisorDecision
                    {
                        Decision = "continue",
                        Why = $"Pré-contrôle valide pour {tool}.",
                        TechnicalSuccess = true,
                        TaskSuccess = true
                    };
                }

                // -------- Création de fichier
                if (tool == "create_bureatique_file")
                {
                    var hasFileType = input.Parameters.TryGetValue("fileType", out var fileTypeObj) &&
                                      !string.IsNullOrWhiteSpace(fileTypeObj?.ToString());

                    var hasFileName = input.Parameters.TryGetValue("fileName", out var fileNameObj) &&
                                      !string.IsNullOrWhiteSpace(fileNameObj?.ToString());

                    var hasSpec = input.Parameters.TryGetValue("specJson", out var specObj) &&
                                  !string.IsNullOrWhiteSpace(specObj?.ToString());

                    return new SupervisorDecision
                    {
                        Decision = (hasFileType && hasFileName && hasSpec) ? "continue" : "replan",
                        Why = (hasFileType && hasFileName && hasSpec)
                            ? "Pré-contrôle valide pour create_bureatique_file."
                            : "Paramètres create_bureatique_file incomplets.",
                        TechnicalSuccess = hasFileType && hasFileName && hasSpec,
                        TaskSuccess = hasFileType && hasFileName && hasSpec
                    };
                }

                // -------- Envoi mail
                if (tool == "send_mail")
                {
                    var hasTo = input.Parameters.TryGetValue("destinataires", out var toObj) &&
                                !string.IsNullOrWhiteSpace(toObj?.ToString());

                    var hasSubject = input.Parameters.TryGetValue("subject", out var subjObj) &&
                                     !string.IsNullOrWhiteSpace(subjObj?.ToString());

                    var hasContent = input.Parameters.TryGetValue("content", out var contentObj) &&
                                     !string.IsNullOrWhiteSpace(contentObj?.ToString());

                    if (!(hasTo && hasSubject && hasContent))
                    {
                        return new SupervisorDecision
                        {
                            Decision = "replan",
                            Why = "Paramètres send_mail incomplets.",
                            TechnicalSuccess = false,
                            TaskSuccess = false
                        };
                    }

                    var hasAttachmentParam =
                        input.Parameters.TryGetValue("attachments", out var attObj) &&
                        !string.IsNullOrWhiteSpace(attObj?.ToString());

                    if (!hasAttachmentParam)
                    {
                        return new SupervisorDecision
                        {
                            Decision = "continue",
                            Why = "Pré-contrôle valide pour send_mail sans pièce jointe.",
                            TechnicalSuccess = true,
                            TaskSuccess = true
                        };
                    }

                    // Si une PJ est annoncée, il faut qu'un fichier ait déjà été généré réellement
                    var fileAlreadyGenerated = input.CompletedSteps.Any(s =>
                        (string.Equals(s.ToolName, "create_bureatique_file", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(s.ToolName, "create_file", StringComparison.OrdinalIgnoreCase))
                        && !string.IsNullOrWhiteSpace(s.ResultJson)
                        && s.ResultJson.Contains("\"filePath\"", StringComparison.OrdinalIgnoreCase));

                    return new SupervisorDecision
                    {
                        Decision = fileAlreadyGenerated ? "continue" : "replan",
                        Why = fileAlreadyGenerated
                            ? "Pré-contrôle valide pour send_mail avec pièce jointe."
                            : "Le mail contient une pièce jointe mais aucun fichier n'a encore été généré.",
                        TechnicalSuccess = fileAlreadyGenerated,
                        TaskSuccess = fileAlreadyGenerated
                    };
                }

                // Cas inconnus : on laisse éventuellement le LLM arbitrer
                return null;
            }

            if (input.Phase == "post_execute")
            {
                if (string.IsNullOrWhiteSpace(input.ResultJson))
                {
                    return new SupervisorDecision
                    {
                        Decision = "replan",
                        Why = "Résultat vide.",
                        TechnicalSuccess = false,
                        TaskSuccess = false
                    };
                }

                var lower = input.ResultJson.ToLowerInvariant();

                if (lower.Contains("\"ok\":false") || lower.Contains("exception"))
                {
                    return new SupervisorDecision
                    {
                        Decision = "replan",
                        Why = "Échec technique détecté.",
                        TechnicalSuccess = false,
                        TaskSuccess = false
                    };
                }

                if (string.Equals(input.ToolName, "sql_query", StringComparison.OrdinalIgnoreCase))
                {
                    var hasRows = lower.Contains("\"rows\"");
                    return new SupervisorDecision
                    {
                        Decision = "continue",
                        Why = hasRows ? "Résultat SQL exploitable." : "Résultat SQL sans lignes.",
                        TechnicalSuccess = true,
                        TaskSuccess = hasRows
                    };
                }

                if (string.Equals(input.ToolName, "create_bureatique_file", StringComparison.OrdinalIgnoreCase))
                {
                    var hasFilePath = lower.Contains("\"filepath\"");
                    return new SupervisorDecision
                    {
                        Decision = hasFilePath ? "continue" : "replan",
                        Why = hasFilePath ? "Fichier généré." : "Fichier non généré.",
                        TechnicalSuccess = hasFilePath,
                        TaskSuccess = hasFilePath
                    };
                }

                if (string.Equals(input.ToolName, "send_mail", StringComparison.OrdinalIgnoreCase))
                {
                    return new SupervisorDecision
                    {
                        Decision = "continue",
                        Why = "Mail traité.",
                        TechnicalSuccess = true,
                        TaskSuccess = true
                    };
                }
            }

            return null;
        }
        private static ToolCall ApplyDeterministicStepAdjustments(ToolCall toolCall, int stepIndex, int totalSteps)
        {
            if (string.Equals(toolCall.ToolName, "create_bureatique_file", StringComparison.OrdinalIgnoreCase)
                && toolCall.Parameters.TryGetValue("openFile", out var openFileObj)
                && openFileObj is string)
            {
                toolCall.Parameters["openFile"] = ((stepIndex + 1 == totalSteps) ? "1" : "0");
            }

            return toolCall;
        }
        private static ToolCall CloneToolCall(ToolCall source)
        {
            return new ToolCall
            {
                ToolName = source.ToolName,
                Parameters = source.Parameters.ToDictionary(k => k.Key, v => v.Value, StringComparer.OrdinalIgnoreCase)
            };
        }
        public async Task<MultiAgentExecutionResult> ExecuteAsync(string userPrompt, List<ConversationMessage> history, string plannerPrompt)
        {
            var result = new MultiAgentExecutionResult
            {
                ExecutionId = Guid.NewGuid().ToString("N")
            };

            RouterPlan plan = new();
            var replanReason = string.Empty;

            while (result.PlanCycles < _budget.MaxPlanCycles)
            {
                result.PlanCycles++;
                Trace("planner", "started", $"Cycle={result.PlanCycles}; Analyse de la demande : {userPrompt}");
                plan = await _planner(userPrompt, BuildPlannerPrompt(plannerPrompt, replanReason, result), history);
                result.Plan = plan;
                Trace("planner", "completed", JsonSerializer.Serialize(plan));
                await _memoryStore.AddObservationAsync("planner", userPrompt, new Dictionary<string, object>(), JsonSerializer.Serialize(plan), technicalSuccess: true, taskSuccess: true, why: replanReason, outcomeKind: "plan", correlationId: result.ExecutionId, stepIndex: result.PlanCycles);

                history.Add(new ConversationMessage { Role = "system", Content = plannerPrompt });

                if (plan.Plan.Count == 0 || string.Equals(plan.Plan[0].ToolName, "none", StringComparison.OrdinalIgnoreCase))
                {
                    Trace("planner", "no_tool", "Aucun outil requis selon le planner.");
                    result.FinalAnswer = await _directResponder(userPrompt);
                    result.Completed = true;
                    return result;
                }

                var cycleOutcome = await ExecutePlanCycleAsync(result, plan, userPrompt, history);
                if (cycleOutcome == PlanCycleOutcome.Completed || cycleOutcome == PlanCycleOutcome.Stopped)
                    break;

                if (cycleOutcome == PlanCycleOutcome.Replan)
                {
                    if (result.ReplanCount >= _budget.MaxReplans)
                    {
                        Trace("supervisor", "replan_budget_exhausted", "Budget de replanification atteint.");
                        result.FinalAnswer = await BuildStopAnswerAsync(userPrompt, result, "Le système a atteint sa limite de replanification.");
                        result.Completed = true;
                        return result;
                    }

                    result.ReplanCount++;
                    replanReason = result.LastSupervisorDecision?.Why ?? "Le superviseur demande une replanification.";
                    Trace("supervisor", "replan", replanReason);
                    continue;
                }
            }

            _toolSelected?.Invoke("");

            if (string.IsNullOrWhiteSpace(result.FinalAnswer))
            {
                Trace("synthesizer", "started", "Synthèse de la réponse finale");
                result.FinalAnswer = await _synthesizer(userPrompt, result.ToolResults);
                await _memoryStore.AddObservationAsync("synthesizer", userPrompt, new Dictionary<string, object>(), result.FinalAnswer ?? string.Empty, technicalSuccess: true, taskSuccess: !result.ToolResults.Any(x => LooksLikeFailure(x.ResultJson)), why: "Synthèse finale", outcomeKind: "final_answer", correlationId: result.ExecutionId, stepIndex: result.ToolResults.Count + 1);
                Trace("synthesizer", "completed", result.FinalAnswer ?? string.Empty);
            }

            result.Completed = true;
            return result;
        }

        private async Task<PlanCycleOutcome> ExecutePlanCycleAsync(MultiAgentExecutionResult result, RouterPlan plan, string userPrompt, List<ConversationMessage> history)
        {
            for (int i = 0; i < plan.Plan.Count; i++)
            {
                if (result.ToolResults.Count >= _budget.MaxToolExecutions)
                {
                    Trace("budget", "tool_execution_limit", $"Limite de {_budget.MaxToolExecutions} exécutions atteinte.");
                    result.FinalAnswer = await BuildStopAnswerAsync(userPrompt, result, "Le budget d'exécution des outils a été atteint.");
                    return PlanCycleOutcome.Stopped;
                }

                var planned = plan.Plan[i];
                if (string.IsNullOrWhiteSpace(planned.ToolName) || planned.ToolName.Equals("none", StringComparison.OrdinalIgnoreCase))
                    continue;

                var toolName = planned.ToolName.Trim();
                _toolSelected?.Invoke(toolName);
                Trace("specialist", "started", $"Préparation step {i + 1}/{plan.Plan.Count} pour {toolName}");

                /*
                var toolCall = await _specialist(toolName, userPrompt, planned.Parameters, history);
                toolCall.ToolName = toolName;
                Trace("specialist", "proposed", JsonSerializer.Serialize(toolCall));
                */
                var toolCall = await _specialist(toolName, userPrompt, planned.Parameters, history);
                toolCall.ToolName = toolName;
                //toolCall = NormalizeToolCall(userPrompt, toolName, toolCall);
                toolCall = ApplyDeterministicStepAdjustments(toolCall, i, plan.Plan.Count);
                toolCall = CloneToolCall(toolCall);
                Trace("specialist", "proposed", JsonSerializer.Serialize(toolCall));

                /*
                var preDecision = await _supervisor(new SupervisorInput
                {
                    Phase = "pre_execute",
                    ExecutionId = result.ExecutionId,
                    UserPrompt = userPrompt,
                    ToolName = toolName,
                    Parameters = toolCall.Parameters,
                    PlanJson = JsonSerializer.Serialize(plan),
                    CompletedSteps = result.ToolResults.Select((x, idx) => new SupervisorStepSnapshot
                    {
                        StepIndex = idx,
                        ToolName = x.ToolName,
                        ResultJson = x.ResultJson
                    }).ToList(),
                    ReplanCount = result.ReplanCount,
                    PlanCycle = result.PlanCycles
                }, history);
                */
                var preInput = new SupervisorInput
                {
                    Phase = "pre_execute",
                    ExecutionId = result.ExecutionId,
                    UserPrompt = userPrompt,
                    ToolName = toolName,
                    Parameters = toolCall.Parameters,
                    PlanJson = JsonSerializer.Serialize(plan),
                    CompletedSteps = result.ToolResults.Select((x, idx) => new SupervisorStepSnapshot
                    {
                        StepIndex = idx,
                        ToolName = x.ToolName,
                        ResultJson = x.ResultJson
                    }).ToList(),
                    ReplanCount = result.ReplanCount,
                    PlanCycle = result.PlanCycles
                };

                // On appelle le superviseur que si le retour est null
                var eval = EvaluateSupervisorDeterministically(preInput);
                var preDecision = eval ?? await _supervisor(preInput, history);
                result.LastSupervisorDecision = preDecision;
                Trace("supervisor", preDecision.Decision, preDecision.Why, eval != null ? "Pre-décision déterministe" : "Pre-décision");

                if (preDecision.Decision == "stop")
                {
                    result.FinalAnswer = await BuildStopAnswerAsync(userPrompt, result, preDecision.Why);
                    return PlanCycleOutcome.Stopped;
                }

                if (preDecision.Decision == "replan")
                    return PlanCycleOutcome.Replan;

                var criticAttempts = 0;
                while (true)
                {
                    var criticDecision = await _critic(toolName, toolCall, userPrompt, history);
                    Trace("critic", criticDecision.Decision.ToLowerInvariant(), criticDecision.Why);

                    if (criticDecision.Decision == "replan")
                    {
                        result.LastSupervisorDecision = new SupervisorDecision
                        {
                            Decision = "replan",
                            Why = string.IsNullOrWhiteSpace(criticDecision.Why) ? "Le critic a demandé une replanification." : criticDecision.Why,
                            TechnicalSuccess = false,
                            TaskSuccess = false
                        };
                        return PlanCycleOutcome.Replan;
                    }

                    if (criticDecision.Decision == "reject")
                    {
                        await _memoryStore.AddObservationAsync($"critic:{toolName}", userPrompt, toolCall.Parameters, "", technicalSuccess: false, taskSuccess: false, why: criticDecision.Why, outcomeKind: "reject", correlationId: result.ExecutionId, stepIndex: i);
                        result.FinalAnswer = await BuildStopAnswerAsync(userPrompt, result, criticDecision.Why);
                        return PlanCycleOutcome.Stopped;
                    }

                    if (criticDecision.Decision != "retry")
                        break;

                    criticAttempts++;
                    if (criticAttempts > _budget.MaxCriticRetriesPerStep)
                    {
                        result.LastSupervisorDecision = new SupervisorDecision
                        {
                            Decision = "replan",
                            Why = $"Trop de retries critic pour {toolName}.",
                            TechnicalSuccess = false,
                            TaskSuccess = false
                        };
                        return PlanCycleOutcome.Replan;
                    }

                    var revised = MergeParameters(toolCall.Parameters, criticDecision.RevisedParameters);
                    Trace("critic", "retry_rebuild", JsonSerializer.Serialize(revised));
                    toolCall = await _specialist(toolName, userPrompt, revised, history);
                    toolCall.ToolName = toolName;
                    Trace("specialist", "reworked", JsonSerializer.Serialize(toolCall));
                }

                var tool = _toolRegistry.GetTool(toolCall.ToolName);
                if (tool == null)
                {
                    var unknownResult = $"{{\"ok\":false,\"error\":\"Outil inconnu : {toolCall.ToolName}\"}}";
                    result.ToolResults.Add((toolCall.ToolName, unknownResult));
                    history.Add(new ConversationMessage { Role = "assistant", Content = unknownResult, Tool = toolCall.ToolName });
                    await _memoryStore.AddObservationAsync(toolCall.ToolName, userPrompt, toolCall.Parameters, unknownResult, technicalSuccess: false, taskSuccess: false, why: "Outil introuvable dans le registre", outcomeKind: "unknown_tool", correlationId: result.ExecutionId, stepIndex: i);
                    result.LastSupervisorDecision = new SupervisorDecision
                    {
                        Decision = "replan",
                        Why = $"Outil introuvable : {toolCall.ToolName}",
                        TechnicalSuccess = false,
                        TaskSuccess = false
                    };
                    return PlanCycleOutcome.Replan;
                }

                Trace("executor", "started", $"Exécution de {toolCall.ToolName}");
                var safeRunner = new SafeToolRunner(tool, _approvalService, _approvalSummary);
                var executed = new HallieCore.Services.ToolCallApprob(toolCall.ToolName, toolCall.Parameters, JsonSerializer.Serialize(toolCall.Parameters));
                _toolCallTrace?.Invoke(toolCall.ToolName, executed.ParametersJson);

                string executionResult;
                try
                {
                    executionResult = await safeRunner.RunAsync(executed) ?? string.Empty;
                }
                catch (Exception ex)
                {
                    executionResult = $"{{\"ok\":false,\"error\":{JsonSerializer.Serialize(ex.Message)}}}";
                }

                Trace("executor", "completed", executionResult ?? string.Empty);

                result.ToolResults.Add((toolCall.ToolName, executionResult ?? string.Empty));
                history.Add(new ConversationMessage { Role = "assistant", Content = executionResult ?? string.Empty, Tool = toolCall.ToolName });

                var evaluation = EvaluateToolOutcome(executionResult ?? string.Empty);
                await _memoryStore.AddObservationAsync(toolCall.ToolName, userPrompt, toolCall.Parameters, executionResult ?? string.Empty, evaluation.TechnicalSuccess, evaluation.TaskSuccess, evaluation.Why, evaluation.OutcomeKind, result.ExecutionId, i);

                /*
                var postDecision = await _supervisor(new SupervisorInput
                {
                    Phase = "post_execute",
                    ExecutionId = result.ExecutionId,
                    UserPrompt = userPrompt,
                    ToolName = toolCall.ToolName,
                    Parameters = toolCall.Parameters,
                    ResultJson = executionResult ?? string.Empty,
                    PlanJson = JsonSerializer.Serialize(plan),
                    CompletedSteps = result.ToolResults.Select((x, idx) => new SupervisorStepSnapshot
                    {
                        StepIndex = idx,
                        ToolName = x.ToolName,
                        ResultJson = x.ResultJson
                    }).ToList(),
                    TechnicalSuccess = evaluation.TechnicalSuccess,
                    TaskSuccess = evaluation.TaskSuccess,
                    ReplanCount = result.ReplanCount,
                    PlanCycle = result.PlanCycles
                }, history);
                */
                var postInput = new SupervisorInput
                {
                    Phase = "post_execute",
                    ExecutionId = result.ExecutionId,
                    UserPrompt = userPrompt,
                    ToolName = toolCall.ToolName,
                    Parameters = toolCall.Parameters,
                    ResultJson = executionResult ?? string.Empty,
                    PlanJson = JsonSerializer.Serialize(plan),
                    CompletedSteps = result.ToolResults.Select((x, idx) => new SupervisorStepSnapshot
                    {
                        StepIndex = idx,
                        ToolName = x.ToolName,
                        ResultJson = x.ResultJson
                    }).ToList(),
                    TechnicalSuccess = evaluation.TechnicalSuccess,
                    TaskSuccess = evaluation.TaskSuccess,
                    ReplanCount = result.ReplanCount,
                    PlanCycle = result.PlanCycles
                };

                var postDecision = EvaluateSupervisorDeterministically(postInput) ?? await _supervisor(postInput, history);
                result.LastSupervisorDecision = postDecision;
                Trace("supervisor", postDecision.Decision, postDecision.Why, "Post-décision");
                await _memoryStore.AddObservationAsync("supervisor", userPrompt, toolCall.Parameters, executionResult ?? string.Empty, postDecision.TechnicalSuccess, postDecision.TaskSuccess, postDecision.Why, postDecision.Decision, result.ExecutionId, i);

                if (postDecision.Decision == "stop")
                {
                    result.FinalAnswer = await BuildStopAnswerAsync(userPrompt, result, postDecision.Why);
                    return PlanCycleOutcome.Stopped;
                }

                if (postDecision.Decision == "replan")
                    return PlanCycleOutcome.Replan;
            }

            return PlanCycleOutcome.Completed;
        }

        private async Task<string> BuildStopAnswerAsync(string userPrompt, MultiAgentExecutionResult result, string why)
        {
            var toolResults = result.ToolResults.ToList();
            if (!string.IsNullOrWhiteSpace(why))
                toolResults.Add(("supervisor", $"{{\"ok\":false,\"message\":{JsonSerializer.Serialize(why)}}}"));

            if (toolResults.Count == 0)
                return await _directResponder(userPrompt + "\n\nContrainte interne : " + why);

            return await _synthesizer(userPrompt, toolResults);
        }

        private static ToolOutcomeEvaluation EvaluateToolOutcome(string executionResult)
        {
            if (string.IsNullOrWhiteSpace(executionResult))
                return new ToolOutcomeEvaluation(false, false, "Résultat vide", "empty");

            var lower = executionResult.ToLowerInvariant();
            var technicalFailure = lower.Contains("\"ok\":false")
                                   //|| lower.Contains("error")
                                   || lower.Contains("exception")
                                   || lower.Contains("action refused")
                                   || lower.Contains("refused by user");

            if (technicalFailure)
                return new ToolOutcomeEvaluation(false, false, "Echec technique détecté", "technical_failure");

            var taskFailure = lower.Contains("impossible")
                            //  || lower.Contains("aucun document")
                              || lower.Contains("aucun fichier")
                              || lower.Contains("introuvable")
                              || lower.Contains("non trouvé")
                              || lower.Contains("not found")
                              || lower.Contains("aucun résultat")
                              || lower.Contains("insuffisant");

            if (taskFailure)
                return new ToolOutcomeEvaluation(true, false, "Succès technique mais objectif non atteint", "task_failure");

            return new ToolOutcomeEvaluation(true, true, "Succès technique et métier", "success");
        }

        private static bool LooksLikeFailure(string executionResult)
        {
            return !EvaluateToolOutcome(executionResult).TaskSuccess;
        }

        private static string BuildPlannerPrompt(string plannerPrompt, string replanReason, MultiAgentExecutionResult result)
        {
            if (string.IsNullOrWhiteSpace(replanReason))
                return plannerPrompt;

            var sb = new StringBuilder();
            sb.AppendLine(plannerPrompt);
            sb.AppendLine();
            sb.AppendLine("Contexte de replanification :");
            sb.AppendLine($"- nombre de cycles de plan déjà tentés : {result.PlanCycles}");
            sb.AppendLine($"- nombre de replanifications : {result.ReplanCount}");
            sb.AppendLine($"- raison : {replanReason}");
            if (result.ToolResults.Count > 0)
            {
                sb.AppendLine("- résultats précédents :");
                foreach (var (tool, json) in result.ToolResults.TakeLast(4))
                    sb.AppendLine($"  * [{tool}] {Trim(json, 220)}");
            }
            return sb.ToString();
        }

        private void Trace(string agentName, string action, string content, string compl="")
        {
            compl = compl == "" ? "" : $"{compl} - ";
            LoggerService.LogDebug($"[MultiAgent:{agentName}] {compl}{action} => {content}");
            _trace?.Invoke(new MultiAgentTrace
            {
                AgentName = agentName,
                Action = action,
                Content = content,
                Timestamp = DateTime.Now
            });
        }

        private static Dictionary<string, object> MergeParameters(Dictionary<string, object>? original, Dictionary<string, object>? revised)
        {
            var merged = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            if (original != null)
            {
                foreach (var kv in original)
                    merged[kv.Key] = kv.Value;
            }

            if (revised != null)
            {
                foreach (var kv in revised)
                    merged[kv.Key] = kv.Value;
            }

            return merged;
        }

        private static string Trim(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            return value.Length <= max ? value : value[..max] + "...";
        }

        private enum PlanCycleOutcome
        {
            Completed,
            Replan,
            Stopped
        }
    }

    public sealed class MultiAgentBudget
    {
        /// <summary>
        /// Le nombre total de cycles de planification (entre 3 et 6)
        /// 5 = compromis robustesse / coût.
        /// </summary>
        public int MaxPlanCycles { get; set; } = 5;

        /// <summary>
        /// Le nombre de fois où le système peut changer complètement de stratégie (entre 2 et 4)
        /// </summary>
        public int MaxReplans { get; set; } = 4;

        /// <summary>
        /// Le nombre de fois où le critic peut demander de corriger les paramètres d’un outil (entre 1 et 2)
        /// Avec 1, le critic n’a qu’une seule correction possible
        /// </summary>
        public int MaxCriticRetriesPerStep { get; set; } = 1;

        /// <summary>
        /// Le nombre total d’outils exécutés dans toute la requête (entre 5 et 10)
        /// </summary>
        public int MaxToolExecutions { get; set; } = 8;
    }

    public sealed class MultiAgentExecutionResult
    {
        public string ExecutionId { get; set; } = Guid.NewGuid().ToString("N");
        public RouterPlan Plan { get; set; } = new();
        public List<(string ToolName, string ResultJson)> ToolResults { get; set; } = new();
        public string FinalAnswer { get; set; } = "";
        public int PlanCycles { get; set; }
        public int ReplanCount { get; set; }
        public bool Completed { get; set; }
        public SupervisorDecision? LastSupervisorDecision { get; set; }
    }

    public sealed class MultiAgentTrace
    {
        public string AgentName { get; set; } = "";
        public string Action { get; set; } = "";
        public string Content { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public sealed class CriticDecision
    {
        public string Decision { get; set; } = "approve";
        public string Why { get; set; } = "";
        public Dictionary<string, object> RevisedParameters { get; set; } = new();
    }

    public sealed class SupervisorInput
    {
        public string Phase { get; set; } = "pre_execute";
        public string ExecutionId { get; set; } = "";
        public string UserPrompt { get; set; } = "";
        public string ToolName { get; set; } = "";
        public Dictionary<string, object> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public string ResultJson { get; set; } = "";
        public string PlanJson { get; set; } = "";
        public bool TechnicalSuccess { get; set; }
        public bool TaskSuccess { get; set; }
        public int ReplanCount { get; set; }
        public int PlanCycle { get; set; }
        public List<SupervisorStepSnapshot> CompletedSteps { get; set; } = new();
    }

    public sealed class SupervisorStepSnapshot
    {
        public int StepIndex { get; set; }
        public string ToolName { get; set; } = "";
        public string ResultJson { get; set; } = "";
    }

    public sealed class SupervisorDecision
    {
        public string Decision { get; set; } = "continue";
        public string Why { get; set; } = "";
        public bool TechnicalSuccess { get; set; } = true;
        public bool TaskSuccess { get; set; } = true;
    }

    public sealed record ToolOutcomeEvaluation(bool TechnicalSuccess, bool TaskSuccess, string Why, string OutcomeKind);

    public sealed class AgentMemoryStore
    {
        private readonly Dictionary<string, List<AgentMemoryEntry>> _entries = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _sync = new();
        private readonly int _maxPerAgent;
        private readonly IAgentMemoryPersistence? _persistence;

        public AgentMemoryStore(IAgentMemoryPersistence? persistence = null, int maxPerAgent = 12)
        {
            _persistence = persistence;
            _maxPerAgent = Math.Max(3, maxPerAgent);
        }

        public async Task AddSuccessAsync(string agentName, string userPrompt, Dictionary<string, object> parameters, string result, CancellationToken ct = default)
            => await AddObservationAsync(agentName, userPrompt, parameters, result, technicalSuccess: true, taskSuccess: true, why: string.Empty, outcomeKind: "success", correlationId: null, stepIndex: null, ct);

        public async Task AddFailureAsync(string agentName, string userPrompt, Dictionary<string, object> parameters, string result, string why, CancellationToken ct = default)
            => await AddObservationAsync(agentName, userPrompt, parameters, result, technicalSuccess: false, taskSuccess: false, why: why, outcomeKind: "failure", correlationId: null, stepIndex: null, ct);

        public async Task AddObservationAsync(string agentName, string userPrompt, Dictionary<string, object> parameters, string result, bool technicalSuccess, bool taskSuccess, string why, string outcomeKind, string? correlationId = null, int? stepIndex = null, CancellationToken ct = default)
            => await AddAsync(agentName, userPrompt, parameters, result, technicalSuccess, taskSuccess, why, outcomeKind, correlationId, stepIndex, ct);

        public IReadOnlyList<AgentMemoryEntry> GetRecentLocal(string agentName, int take = 4)
        {
            lock (_sync)
            {
                if (!_entries.TryGetValue(agentName, out var list))
                    return Array.Empty<AgentMemoryEntry>();

                return list.TakeLast(Math.Max(1, take)).ToList();
            }
        }

        public async Task<IReadOnlyList<AgentMemoryEntry>> GetRecentAsync(string agentName, int take = 4, CancellationToken ct = default)
        {
            var local = GetRecentLocal(agentName, take * 2);
            if (_persistence == null)
                return local.TakeLast(Math.Max(1, take)).ToList();

            try
            {
                var persisted = await _persistence.GetRecentAsync(agentName, take * 2, ct);
                var merged = persisted
                    .Concat(local)
                    .OrderBy(x => x.Timestamp)
                    .GroupBy(x => $"{x.AgentName}|{x.Timestamp:O}|{x.UserPrompt}|{x.Result}|{x.CorrelationId}|{x.StepIndex}")
                    .Select(g => g.Last())
                    .TakeLast(Math.Max(1, take))
                    .ToList();
                return merged;
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"AgentMemoryStore.GetRecentAsync : {ex.Message}");
                return local.TakeLast(Math.Max(1, take)).ToList();
            }
        }

        public async Task<string> BuildMemoryContextAsync(string agentName, int take = 4, CancellationToken ct = default)
        {
            var items = await GetRecentAsync(agentName, take, ct);
            if (items.Count == 0)
                return "Aucune mémoire dédiée disponible pour cet agent.";

            var sb = new StringBuilder();
            sb.AppendLine("Mémoire dédiée récente de cet agent :");
            foreach (var item in items)
            {
                sb.AppendLine($"- [technique={(item.TechnicalSuccess ? "OK" : "KO")}; métier={(item.TaskSuccess ? "OK" : "KO")}] question={item.UserPrompt}");
                sb.AppendLine($"  paramètres={JsonSerializer.Serialize(item.Parameters)}");
                if (!string.IsNullOrWhiteSpace(item.OutcomeKind))
                    sb.AppendLine($"  issue={item.OutcomeKind}");
                if (!string.IsNullOrWhiteSpace(item.Why))
                    sb.AppendLine($"  raison={item.Why}");
                sb.AppendLine($"  résultat={Trim(item.Result, 300)}");
            }
            return sb.ToString();
        }

        private async Task AddAsync(string agentName, string userPrompt, Dictionary<string, object> parameters, string result, bool technicalSuccess, bool taskSuccess, string why, string outcomeKind, string? correlationId, int? stepIndex, CancellationToken ct)
        {
            var entry = new AgentMemoryEntry
            {
                AgentName = agentName,
                UserPrompt = userPrompt,
                Parameters = new Dictionary<string, object>(parameters, StringComparer.OrdinalIgnoreCase),
                Result = result,
                TechnicalSuccess = technicalSuccess,
                TaskSuccess = taskSuccess,
                IsSuccess = technicalSuccess && taskSuccess,
                Why = why,
                OutcomeKind = outcomeKind,
                CorrelationId = correlationId,
                StepIndex = stepIndex,
                Timestamp = DateTime.UtcNow
            };

            lock (_sync)
            {
                if (!_entries.TryGetValue(agentName, out var list))
                {
                    list = new List<AgentMemoryEntry>();
                    _entries[agentName] = list;
                }

                list.Add(entry);
                while (list.Count > _maxPerAgent)
                    list.RemoveAt(0);
            }

            if (_persistence == null)
                return;

            try
            {
                await _persistence.SaveAsync(entry, ct);
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"AgentMemoryStore.AddAsync : {ex.Message}");
            }
        }

        private static string Trim(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            return value.Length <= max ? value : value[..max] + "...";
        }
    }

    public sealed class AgentMemoryEntry
    {
        public string AgentName { get; set; } = "";
        public string UserPrompt { get; set; } = "";
        public Dictionary<string, object> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public string Result { get; set; } = "";
        public bool IsSuccess { get; set; }
        public bool TechnicalSuccess { get; set; } = true;
        public bool TaskSuccess { get; set; } = true;
        public string Why { get; set; } = "";
        public string OutcomeKind { get; set; } = "";
        public string? CorrelationId { get; set; }
        public int? StepIndex { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
