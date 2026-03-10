using ExternalServices;
using Hallie.Services;
using System.ComponentModel;
using System.Text;
using HallieDomain;

namespace Hallie.Core
{
    public class AiOrchestrator : INotifyPropertyChanged
    {
        #region Events
        public event Action<(AiState, string?)>? StateChanged;
        public event Action<string>? LlmToken;
        #endregion

        #region Propriétés
        public AiState State { get; private set; } = AiState.Waiting;
        public string? _Message { get; private set; } = "";

        private string _outilUsed = "";

        public string OutilUsed
        {
            get => _outilUsed;
            set
            {
                if (_outilUsed != value)
                {
                    _outilUsed = value;
                    var outil = _outilUsed != "" ? $"{_outilUsed}" : "aucun";
                    LoggerService.LogDebug($"AiOrchestrator.OutilUsed : {outil}");
                    OnPropertyChanged(nameof(OutilUsed));
                }
                if (_outilUsed != "")
                    LastTool.Add(_outilUsed);
            }
        }
        #endregion

        #region Variables
        private List<string> LastTool = new();
        private static readonly string[] WakeWords =
        {
            $"ok",
            $"hey",
            $"salut",
            $"bonjour"
        };

        private static string Normalize(string text)
        {
            return text
                .ToLowerInvariant()
                .Replace(",", "")
                .Replace(".", "")
                .Replace("!", "")
                .Replace("?", "")
                .Trim();
        }

        private static bool ContainsWakeWord(string text)
        {
            var normalized = Normalize(text);

            return WakeWords.Any(w =>
                normalized.StartsWith(w) ||
                normalized.Contains(" " + w));
        }

        private readonly ISpeechToText _stt;
        private readonly ITextToSpeech _tts;
        private readonly OllamaClient _llm;
        private readonly List<ConversationMessage> _conversationHistory = new();

        // Dernier tour (utile pour feedback explicite)
        private string _lastConversationId = "";
        private int _lastTurnId = 0;
        private string _lastUserPrompt = "";

        private CancellationTokenSource? _currentCts;
        private readonly object _lock = new object();
        #endregion

        #region Constructeur
        public AiOrchestrator(ISpeechToText stt, ITextToSpeech tts, OllamaClient llm)
        {
            LoggerService.LogInfo($"AiOrchestrator");

            _stt = stt;
            _tts = tts;
            _llm = llm;

            WireEvents();
        }
        #endregion

        #region Méthodes publiques
        private string _currentSpokenText = "";
        public async Task<string> AskAsync(string userInput, bool isShowUserInput, ChatConversation? selectedConversation, bool isSaveConv)
        {
            LoggerService.LogInfo($"AiOrchestrator.AskAsync : {userInput}");
            LastTool.Clear();
            try
            {
                lock (_lock)
                {
                    _currentCts?.Cancel();
                    _currentCts = new CancellationTokenSource();
                }

                var ct = _currentCts.Token;

                // On fait une RAZ de l'historique pour chaque question
                _conversationHistory.Clear();

                if (selectedConversation != null)
                {
                    // Charger l'historique de la conversation sélectionnée
                    foreach (var msg in selectedConversation.Messages)
                    {
                        _conversationHistory.Add(new ConversationMessage
                        {
                            Role = msg.Role,
                            Content = msg.Content,
                            Timestamp = msg.Timestamp,
                            Id = msg.Id,
                            //IsGoodTool = msg.IsGoodTool,
                            //Why = msg.Why,
                            //Feedback = msg.Feedback,
                            Tool = msg.Tool
                        });
                    }
                }
                else
                {

                    selectedConversation = new();
                    selectedConversation.Id = Guid.NewGuid().ToString();
                }

                // Tracking pour feedback
                _lastConversationId = selectedConversation.Id;

                var conversation_message_id = Guid.NewGuid();
                // Ajouter la question à l'historique
                _conversationHistory.Add(new ConversationMessage
                {
                    Role = "user",
                    Content = userInput,
                    Timestamp = DateTime.UtcNow,
                    Id = conversation_message_id.ToString()
                });

                _lastUserPrompt = userInput;
                _lastTurnId = _conversationHistory.Count(m => m.Role == "user");

                SetState(AiState.Thinking, "");

                var responseBuilder = new StringBuilder();

                // Utiliser la nouvelle méthode avec outils
                var repGenerator = _llm.GenerateWithToolsAsync(userInput, _conversationHistory);
                await foreach (var chunk in repGenerator)
                {
                    if (ct.IsCancellationRequested)
                        return "";

                    responseBuilder.Append(chunk);
                    LlmToken?.Invoke(chunk);
                }

                var response = responseBuilder.ToString().Trim();
                response = response.Replace("*", "");

                // Ajouter la réponse à l'historique
                _conversationHistory.Add(new ConversationMessage
                {
                    Role = "assistant",
                    Content = response,
                    Timestamp = DateTime.UtcNow,
                    Id = conversation_message_id.ToString(),
                    Tool = string.Join(",", LastTool)
                });

                // Limiter l'historique (garder les x derniers échanges)
                if (_conversationHistory.Count > 150)
                    _conversationHistory.RemoveRange(0, _conversationHistory.Count - 20);

                // Sauvegarde de l'historique dans la conversation sélectionnée
                selectedConversation.Messages.Clear();
                foreach (var conversation in _conversationHistory)
                {
                    selectedConversation.Messages.Add(new ConversationMessage
                    {
                        Role = conversation.Role,
                        Content = conversation.Content,
                        Timestamp = conversation.Timestamp,
                        Id = conversation.Id,
                        Tool = conversation.Tool,
                        //IsGoodTool = conversation.IsGoodTool,
                        //Why = conversation.Why,
                        //Feedback = conversation.Feedback
                    });
                }
                if(selectedConversation.Messages.Count>0)
                {
                    var msg = selectedConversation.Messages[0].Content;
                    msg = msg.Replace("\n", " ").Replace("\r", " ").Trim();

                    selectedConversation.Title = msg.Length > 30
                        ? msg.Substring(0, 30) + "..." + DateTime.Now.ToShortDateString()
                        : msg + "..." + DateTime.Now.ToShortDateString();

                    selectedConversation.TitleLong = msg.Length > 80
                        ? msg.Substring(0, 80) + "..." + DateTime.Now.ToShortDateString()
                        : msg + "..." + DateTime.Now.ToShortDateString();

                }
                if (isSaveConv)
                {
                    // Enregistrer la conversation (cela écrasera l'ancienne version de cette conversation)

                    ConversationsService.SaveConversation(selectedConversation);

                    // Ingestion de la conversation dans Qdrant
                    /*
                    var qdrantService = new QdrantService(
                        new OllamaEmbeddingService(Params.OllamaEmbeddingUrl!,Params.OllamaEmbeddingModel!),
                        Params.QdrantUrl!,
                        "Conversation");
                    
                    var b = await qdrantService.IngestConversationAsync(conversation_message_id.ToString(), userInput, response, string.Join(",", LastTool), Params.SmtpUser!);
                    if(b)
                        LoggerService.LogDebug($"AiOrchestrator.AskAsync : conversation ingérée dans Qdrant");
                    else
                        LoggerService.LogWarning($"AiOrchestrator.AskAsync : échec ingestion conversation dans Qdrant");
                    LastTool.Clear();
                    */
                }
                else
                {
                    selectedConversation.Messages.Clear();
                }

                // Activer le mode mot-clé AVANT de parler
                // Cela empêche le système de s'auto-interrompre en entendant sa propre voix
                _stt.EnableKeywordOnlyMode();

                var showReponse = response;
                if (isShowUserInput)
                    showReponse = $"[UTILISATEUR] : {userInput}  \n\n[ASSISTANT] : {response}";
                _currentSpokenText = showReponse;

                // On change AiState.Speaking en AiState.Thinking
                // Ainsi, le traitement du fichier audio à lire se faire pendant "Thinking"
                // "Speaking" arrivera au moment de la lecture du fichier audio
                // cf. méthode WireEvents() -->
                //      _tts.SpeechStarted += () =>
                //      {
                //          SetState(AiState.Speaking, _currentSpokenText);
                //      };
                //SetState(AiState.Speaking, showReponse);
                SetState(AiState.Thinking, showReponse);

                await _tts.SpeakAsync(response, ct);

                // Désactiver le mode mot-clé APRÈS avoir parlé
                _stt.DisableKeywordOnlyMode();

                SetState(AiState.Waiting, "");
                return response;
            }

            catch (OperationCanceledException)
            {
                LoggerService.LogError("[Orchestrator] Opération annulée");

                // IMPORTANT : Toujours revenir en mode normal
                _stt.DisableKeywordOnlyMode();

                SetState(AiState.Interrupted, "");

                // Retour à Idle après une courte pause
                await Task.Delay(500);
                SetState(AiState.Waiting, "");
                return "[Orchestrator] Opération annulée";
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"AiOrchestrator.AskAsync --> Erreur: {ex.Message}");

                // IMPORTANT : Toujours revenir en mode normal en cas d'erreur
                _stt.DisableKeywordOnlyMode();

                SetState(AiState.Waiting, "");
                return ex.Message;
            }
        }

        public IReadOnlyList<OllamaClient.ToolCallTrace> GetLastExecutedToolCalls()
        {
            return _llm.LastExecutedToolCalls.ToList();
        }

        /// <summary>
        /// Appelable depuis l'UI : enregistre un feedback explicite utilisateur pour améliorer le routage.
        ///
        /// Exemple d'usage côté UI :
        ///  await orchestrator.SubmitRoutingFeedbackAsync(5, "solved", "nickel", expectedTool:null);
        /// </summary>
        public async Task<List<Guid>?> SubmitRoutingFeedbackAsync(
            int userRating,
            string outcome,
            string? comment = null,
            string? expectedTool = null,
            string? errorClass = null,
            List<ToolFeedbackEntry>? perToolEntries = null,
            CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_lastConversationId) || string.IsNullOrWhiteSpace(_lastUserPrompt))
                    return null;

                // ToolUsed compact
                var tools = _llm.LastExecutedToolCalls.Select(t => t.ToolName).ToList();
                var toolUsed = tools.Count == 0 ? "none" : string.Join(">", tools);

                var toolCallsJson = System.Text.Json.JsonSerializer.Serialize(_llm.LastExecutedToolCalls);

                var db = new FeedbackDbService(Params.BddHallieConnexionString!);
                var embedding = new OllamaEmbeddingService(Params.OllamaEmbeddingUrl!, Params.OllamaEmbeddingModel!);
                var indexVectoriel = new FeedbackRoutingIndexService(embedding, Params.QdrantUrl!);
                var svc = new FeedbackLogService(db, indexVectoriel);

                var ids = await svc.LogWithDetailsAsync(
                    conversationId: _lastConversationId,
                    turnId: _lastTurnId,
                    chainUserRating: userRating,
                    chainOutcome: outcome,
                    chainToolUsed: toolUsed,
                    chainToolCallsJson: toolCallsJson,
                    promptText: _lastUserPrompt,
                   // stepIndex:null,
                    chainExpectedTool: expectedTool,
                    chainComment: comment,
                    chainErrorClass: errorClass,
                    perToolEntries: perToolEntries,
                    ct: ct);

                return ids;
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"AiOrchestrator.SubmitRoutingFeedbackAsync : {ex.Message}");
                return null;
            }
        }
        public async Task<string> AskAsync(HallieDomain.AiOrchestrorModel echange)
        {
            string userInput = echange.UserInput;
            bool isShowUserInput = echange.IsShowUserInput;
            HallieDomain.ChatConversation? selectedConversation = echange.SelectedConversation;
            bool isSaveConv = echange.IsSaveConv;
            return await AskAsync(userInput, isShowUserInput, selectedConversation, isSaveConv);
        
        }
        #endregion

        #region Méthodes privées
        private void WireEvents()
        {
            LoggerService.LogInfo($"AiOrchestrator.WireEvents");

            // Événements LLM (choix de l'outil)
            _llm.ToolSelected += toolName =>
            {
                OutilUsed = toolName;
                // Optionnel : notifier l'UI
                StateChanged?.Invoke((AiState.Thinking, $"Outil utilisé : {toolName}"));
            };

            // Événements audio (informatifs uniquement, pas utilisés pour l'interruption)
            _stt.SpeechStarted += () =>
            {
                LoggerService.LogDebug("[Orchestrator] Audio détecté au micro");
            };

            _stt.SpeechEnded += () =>
            {
                LoggerService.LogDebug("[Orchestrator] Fin audio micro");
            };

            // Résultat de transcription (mode normal uniquement)
            _stt.FinalResult += tuple => OnUserFinalText(tuple.Item1, tuple.Item2);

            // NOUVEAU : Mot-clé d'interruption détecté (mode mot-clé uniquement)
            _stt.InterruptKeywordDetected += OnInterruptKeyword;

            // Pour le TTS, on change l'état à Speaking au début de la parole, et on revient à Waiting à la fin
            _tts.SpeechStarted += () =>
            {
                LoggerService.LogDebug("[Orchestrator] Debut speaking");
                SetState(AiState.Speaking, _currentSpokenText);
            };

            _tts.SpeechEnded += () =>
            {
                LoggerService.LogDebug("[Orchestrator] Fin speaking");
                SetState(AiState.Waiting, "");
            };
        }

        private void SetState(AiState state, string? message)
        {
            LoggerService.LogInfo($"AiOrchestrator.SetState");

            if ((State != state) || (_Message != message))
            {
                var msg = (message != null && message.Trim().Length > 10) ? $"{message.Substring(0, 10)}..." : message;
                LoggerService.LogDebug($"AiOrchestrator.SetState --> État: de {State} à {state} - message: {msg}");
                State = state;
                StateChanged?.Invoke((state, message));
            }
            _Message = message;
        }

        private void OnUserFinalText(string text, ChatConversation selectedConversation)
        {
            LoggerService.LogInfo($"AiOrchestrator.OnUserFinalText : {text}");

            if (string.IsNullOrWhiteSpace(text))
            {
                LoggerService.LogDebug($"AiOrchestrator.OnUserFinalText : texte vide");
                return;
            }

            var cleaned = text;
            // Lancer le traitement de manière asynchrone
            _ = Task.Run(() => AskAsync(cleaned, true, selectedConversation, true));
        }

        private static string RemoveWakeWord(string text)
        {
            var normalized = Normalize(text);

            foreach (var w in WakeWords)
            {
                if (normalized.StartsWith(w))
                    return text.Substring(w.Length).Trim();
            }

            return text;
        }

        // Gestionnaire pour l'interruption par mot-clé
        private void OnInterruptKeyword()
        {
            LoggerService.LogInfo($"AiOrchestrator.OnInterruptKeyword");

            // Annuler la tâche en cours (génération LLM ou TTS)
            lock (_lock)
            {
                _currentCts?.Cancel();
            }

            // Arrêter le TTS immédiatement
            _tts.Stop();

            // Revenir en mode normal (écoute de tout)
            _stt.DisableKeywordOnlyMode();

            SetState(AiState.Interrupted, "");

            // Retour à Idle après une courte pause
            _ = Task.Run(async () =>
            {
                await Task.Delay(500);
                SetState(AiState.Waiting, "");
            });
        }
        #endregion

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }
}