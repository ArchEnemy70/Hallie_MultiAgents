using ExternalServices;
using Hallie.Core;
using Hallie.Services;
using Hallie.Tools;
using HallieCore.Services;
using HallieDomain;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
//using Google.Apis.Auth.OAuth2;
//using Google.Apis.Util.Store;

namespace Hallie.ViewModels
{
    public class AvatarViewModel : INotifyPropertyChanged, IDisposable
    {
        #region Services (privés)
        private readonly AiOrchestrator _orchestrator;
        private readonly ITextToSpeech _tts;
        private readonly ISpeechToText _stt;
        private readonly System.Timers.Timer _speakingTimer;
        private ToolRegistry _toolRegistry;
        #endregion

        #region Commands
        public ICommand ToggleMicCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand LireTextSaisiCommand { get; }
        public ICommand FeedbackUpCommand { get; }
        public ICommand FeedbackDownCommand { get; }
        #endregion

        #region Propriétés publiques
        public static ChatConversation? SelectedConversation { get; set; }
        #endregion

        #region Propriétés Bindables
        public string? NumVersion { get; private set; } = "";
        public string ToolsDescription { get; set; } = "";

        private string _TxtSaisi = "";
        public string TxtSaisi
        {
            get => _TxtSaisi;
            set { _TxtSaisi = value; Notify(nameof(TxtSaisi)); }
        }

        private string _feedbackComment = "";
        public string FeedbackComment
        {
            get => _feedbackComment;
            set { _feedbackComment = value; Notify(nameof(FeedbackComment)); }
        }

        private string _feedbackChainDisplay = "";
        public string FeedbackChainDisplay
        {
            get => _feedbackChainDisplay;
            set { _feedbackChainDisplay = value; Notify(nameof(FeedbackChainDisplay)); }
        }
        private bool _IsFeedbackPositif;
        public bool IsFeedbackPositif
        {
            get => _IsFeedbackPositif;
            set { _IsFeedbackPositif = value; Notify(nameof(IsFeedbackPositif)); }
        }
        public bool IsFeedbackNegatif
        {
            get => !IsFeedbackPositif;
        }

        public ObservableCollection<FeedbackToolItemViewModel> FeedbackItems { get; } = new();

        // ===== Feedback avancé (dropdown + rating) =====
        public ObservableCollection<int> FeedbackRatingOptions { get; } = new(new[] { 1, 2, 3, 4, 5 });

        private int _selectedFeedbackRating = 5;
        public int SelectedFeedbackRating
        {
            get => _selectedFeedbackRating;
            set { _selectedFeedbackRating = value; Notify(nameof(SelectedFeedbackRating)); }
        }

        private ObservableCollection<string> _ExpectedToolOptions = new();
        public ObservableCollection<string> ExpectedToolOptions
        {
            get => _ExpectedToolOptions;
            set { _ExpectedToolOptions = value; Notify(nameof(ExpectedToolOptions)); }
        }

        private string? _selectedExpectedTool;
        public string? SelectedExpectedTool
        {
            get => _selectedExpectedTool;
            set { _selectedExpectedTool = value; Notify(nameof(SelectedExpectedTool)); }
        }

        private ObservableCollection<string> _ErrorClassOptions = new(new[]
        {
            "bad_result",
            "bad_route",
            "wrong_data",
            "timeout",
            "hallucination",
            "tool_error",
            "permission",
            "format",
            "other"
        });

        public ObservableCollection<string> ErrorClassOptions
        {
            get => _ErrorClassOptions;
            set { _ErrorClassOptions = value; Notify(nameof(ErrorClassOptions)); }
        }

        private string _selectedErrorClass = "bad_result";
        public string SelectedErrorClass
        {
            get => _selectedErrorClass;
            set { _selectedErrorClass = value; Notify(nameof(SelectedErrorClass)); }
        }

        private bool _canSendFeedback;
        public bool CanSendFeedback
        {
            get => _canSendFeedback;
            private set
            {
                if (_canSendFeedback != value)
                {
                    _canSendFeedback = value;
                    Notify(nameof(CanSendFeedback));
                    (_feedbackUpCmd as RelayCommand)?.RaiseCanExecuteChanged();
                    (_feedbackDownCmd as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        private AiState _state = AiState.Waiting;
        public string State => _state.ToString();

        public string OutilUsed => _orchestrator.OutilUsed;

        public double WaitingOpacity { get; private set; }
        public double ListeningOpacity { get; private set; }
        public double SpeakingOpacity { get; private set; }
        public double Speaking2Opacity { get; private set; }
        public double ThinkingOpacity { get; private set; }
        public double InterruptedOpacity { get; private set; }

        private bool _isMicOn = false;
        public bool IsMicOn
        {
            get => _isMicOn;
            set
            {
                if (_isMicOn != value)
                {
                    _isMicOn = value;
                    Notify(nameof(IsMicOn));
                    _ = HandleMicToggleAsync(value);
                }
            }
        }
        private bool _IsMicroOnManuel = true;

        private bool _isInitialized = false;
        public bool IsInitialized
        {
            get => _isInitialized;
            private set { _isInitialized = value; Notify(nameof(IsInitialized)); }
        }
        #endregion

        #region Variables privées
        private double _audioLevel;
        #endregion

        private readonly IApprovalService _approval;
        private readonly IApprovalSummaryBuilder _approvalSummary;

        private readonly ICommand _feedbackUpCmd;
        private readonly ICommand _feedbackDownCmd;

        #region Constructeur
        public AvatarViewModel(IApprovalService approval, IApprovalSummaryBuilder approvalSummary)
        {
            LoggerService.LogInfo("AvatarViewModel.AvatarViewModel");
            _approval = approval;
            _approvalSummary = approvalSummary;
            NumVersion = FileVersionInfo
                .GetVersionInfo(Environment.ProcessPath!)
                .FileVersion;

            // Charger les paramètres
            LoadParams();

            _toolRegistry = new ToolRegistry();
            // Créer les services
            InitializeServices(out _tts, out _stt, out _orchestrator);
            _orchestrator.PropertyChanged += Orchestrator_PropertyChanged;

            // Configurer les commandes
            ToggleMicCommand = new RelayCommand(
                execute: () => ChangeMicroStatus()
            );

            ResetCommand = new RelayCommand(
                execute: async () => await ResetAsync()
            );

            LireTextSaisiCommand = new RelayCommand(
                execute: async () => await LireTextSaisiAsync()
            );

            _feedbackUpCmd = new RelayCommand(
                execute: async () => await SubmitFeedbackAsync(isPositive: true),
                canExecute: () => CanSendFeedback
            );
            FeedbackUpCommand = _feedbackUpCmd;

            _feedbackDownCmd = new RelayCommand(
                execute: async () => await SubmitFeedbackAsync(isPositive: false),
                canExecute: () => CanSendFeedback
            );
            FeedbackDownCommand = _feedbackDownCmd;
            

            // Timer pour l'animation de parole
            _speakingTimer = new System.Timers.Timer(500);
            _speakingTimer.Elapsed += (s, e) => SpeakingFace();

            // Configurer les événements des services
            WireServiceEvents();

            _Timer = new DispatcherTimer();
            if (Params.DelaiVerifAutoDeadlines.HasValue && Params.DelaiVerifAutoDeadlines! > 0)
            {
                _Timer.Interval = TimeSpan.FromMinutes(Params.DelaiVerifAutoDeadlines!.Value);
                _Timer.Tick += async (s, e) => await TachesRecurrentes();
                _Timer.Start();
            }

            // Par défaut, pas de feedback possible tant qu'un tour utilisateur n'a pas été exécuté
            CanSendFeedback = false;
        }
        #endregion

        #region Feedback UI

        public void PrepareFeedbackEntries()
        {
            FeedbackItems.Clear();

            var traces = _orchestrator.GetLastExecutedToolCalls();
            var defaultMode = IsFeedbackPositif ? "positive" : "negative";

            foreach (var trace in traces)
            {
                FeedbackItems.Add(new FeedbackToolItemViewModel
                {
                    ToolName = trace.ToolName,
                    ParametersJson = trace.ParametersJson,
                    OutcomeMode = defaultMode,
                    SelectedExpectedTool = "(aucun)",
                    SelectedErrorClass = "bad_result"
                });
            }

            var tools = traces.Select(t => t.ToolName).Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
            FeedbackChainDisplay = tools.Count == 0 ? "none" : string.Join(" > ", tools);
        }

        public async Task SubmitFeedbackAsync(bool isPositive)
        {
            try
            {
                var chainRating = SelectedFeedbackRating;
                if (chainRating < 1) chainRating = 1;
                if (chainRating > 5) chainRating = 5;

                if (isPositive && chainRating < 3)
                    chainRating = 5;
                if (!isPositive && chainRating > 2)
                    chainRating = 1;

                var chainOutcome = isPositive ? "solved" : "failed";
                var chainErrorClass = isPositive ? null : (string.IsNullOrWhiteSpace(SelectedErrorClass) ? "bad_result" : SelectedErrorClass);

                string? chainExpectedTool = null;
                if (!string.IsNullOrWhiteSpace(SelectedExpectedTool) && SelectedExpectedTool != "(aucun)")
                    chainExpectedTool = SelectedExpectedTool;

                var perToolEntries = FeedbackItems
                    .Where(x => !string.Equals(x.OutcomeMode, "skip", StringComparison.OrdinalIgnoreCase))
                    .Select(x => new ToolFeedbackEntry
                    {
                        ToolName = x.ToolName,
                        ToolParamsJson = string.IsNullOrWhiteSpace(x.ParametersJson) ? "{}" : x.ParametersJson,
                        UserRating = string.Equals(x.OutcomeMode, "positive", StringComparison.OrdinalIgnoreCase) ? 5 : 1,
                        Outcome = string.Equals(x.OutcomeMode, "positive", StringComparison.OrdinalIgnoreCase) ? "solved" : "failed",
                        ErrorClass = string.Equals(x.OutcomeMode, "positive", StringComparison.OrdinalIgnoreCase)
                            ? null
                            : (string.IsNullOrWhiteSpace(x.SelectedErrorClass) ? "bad_result" : x.SelectedErrorClass),
                        ExpectedTool = !string.IsNullOrWhiteSpace(x.SelectedExpectedTool) && x.SelectedExpectedTool != "(aucun)"
                            ? x.SelectedExpectedTool
                            : null,
                        Comment = string.IsNullOrWhiteSpace(x.Comment) ? null : x.Comment,
                        StepIndex = x.StepIndex
                    })
                    .ToList();

                var ids = await _orchestrator.SubmitRoutingFeedbackAsync(
                    userRating: chainRating,
                    outcome: chainOutcome,
                    comment: string.IsNullOrWhiteSpace(FeedbackComment) ? null : FeedbackComment,
                    expectedTool: chainExpectedTool,
                    errorClass: chainErrorClass,
                    perToolEntries: perToolEntries);

                CanSendFeedback = false;
                FeedbackComment = "";
                SelectedFeedbackRating = 5;
                SelectedExpectedTool = "(aucun)";
                SelectedErrorClass = "bad_result";
                FeedbackItems.Clear();
                FeedbackChainDisplay = "";

                if (ids is null || ids.Count == 0)
                    LoggerService.LogDebug("Feedback non enregistré (pas de dernier tour).");
                else
                    LoggerService.LogInfo($"Feedback enregistré: {string.Join(",", ids)}");
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"AvatarViewModel.SubmitFeedbackAsync : {ex.Message}");
            }
        }
        #endregion

        #region Autres méthodes privées
        private async Task<bool> IsServiceActif(string adressOrIp)
        {
            //LoggerService.LogInfo($"AvatarViewModel.IsServiceActif : {adressOrIp}");

            try
            {
                var http = new HttpClient();
                var response = await http.GetAsync(adressOrIp);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"AvatarViewModel.IsServiceActif : {adressOrIp} --> {ex.Message}");
                return false;
            }

        }
        private async Task<(bool, string)> IsServicesActifs()
        {
            LoggerService.LogInfo($"AvatarViewModel.IsServicesActifs");

            var isActifOllama = await IsServiceActif(Params.OllamaLlmUrl!);
            LoggerService.LogDebug($"AvatarViewModel.IsServicesActifs --> isActifOllama : {isActifOllama}");

            var isActifInternet = await IsServiceActif("https://google.com");
            LoggerService.LogDebug($"AvatarViewModel.IsServicesActifs --> isActifInternet : {isActifInternet}");

            var isActifQdrant = await IsServiceActif(Params.QdrantDashboardUrl!);
            LoggerService.LogDebug($"AvatarViewModel.IsServicesActifs --> isActifQdrant : {isActifQdrant}");

            var cnx = new FeedbackDbService(Params.BddHallieConnexionString);
            var isBddHallieActif = await cnx.IsBddActif();
            LoggerService.LogDebug($"AvatarViewModel.IsServicesActifs --> isBddActif : {isBddHallieActif}");

            var isActifBdds = true;
            StringBuilder sb = new();
            if(Params.BddConnexionsString != null && Params.BddConnexionsString.Count >0)
            {
                foreach(var cnn in Params.BddConnexionsString)
                {
                    var isBdd = await cnx.IsBddActif(cnn.ConnexionString);
                    if (!isBdd)
                    {
                        sb.AppendLine($"Base de données {cnn.BddName}");
                        LoggerService.LogDebug($"AvatarViewModel.IsServicesActifs --> Bdd KO : {cnn.BddName}");
                    }
                    isActifBdds = isActifBdds && isBdd;
                }
            }


            string msg = "";
            if (!isActifOllama)
                msg += "Le service suivant n'est pas accessible : Ollama\n";
            if (!isActifInternet)
                msg += "Le service suivant n'est pas accessible : Internet\n";
            if (!isActifQdrant)
                msg += "Le service suivant n'est pas accessible : Qdrant\n";
            if (!isBddHallieActif)
                msg += "Le service suivant n'est pas accessible : Base de données Feedbacks\n";
            if(!isActifBdds)
                msg += $"Les bases de données suivantes ne sont pas accessibles : {sb.ToString()}\n";


            return (isActifOllama, msg);
        }
        private void Orchestrator_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AiOrchestrator.OutilUsed))
            {
                Notify(nameof(OutilUsed));
            }
        }

        #endregion

        #region Tests
        private async void Tests()
        {
            //await TestDeadlines();
            //await TestApi();
            //await Test_CalendarCreate();
            //await Test_CalendarSearch();
            //await Test_n8n();
            //var (b, lstG, lstB)=await Test_SearchRAG("Quelle est la météo à Sauve ?");
            //Test_vLLM();
            //Test_road();
            //Test_pptx();
            //Test_xlsx();
            //Test_docx();
        }
        private async Task TestDeadlines()
        {
            var prompt = $"regarde dans mes notes et indique moi toutes les deadlines comprises entre {DateTime.Now.ToString("dd/MM/yyyy HH:mm")} et {DateTime.Now.AddHours(+1).ToString("dd/MM/yyyy HH:mm")} (avec le sujet)";
            prompt += "\n" + $"regarde dans mon calendrier et indique moi les événements pour {DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")} à {DateTime.Now.Date.ToString("dd/MM/yyyy")} 23:59";
            var conv = new ChatConversation();
            var r = await _orchestrator.AskAsync(prompt, false, conv, false);
            LoggerService.LogDebug($"{r}");
        }
        private static async Task TestApi()
        {
            HttpClient _httpClient = new();
            var _baseUrl = "https://localhost:7022"
                ?? throw new InvalidOperationException("Url manquante dans CurrentEnvironment");

            var _httpClientLogin = new HttpClient { BaseAddress = new Uri(_baseUrl) };
            _httpClient = new HttpClient()
            {
                BaseAddress = new Uri(_baseUrl)
            };


            AiOrchestrorModel o = new();
            o.IsSaveConv = false;
            o.SelectedConversation = new();
            o.IsShowUserInput = false;
            o.UserInput = "quelle est la météo à Sauve";
            var response = await _httpClient.PostAsJsonAsync("HallieAiOchestror", o);
        }
        private static async Task Test_CalendarCreate()
        {
            var start = "2026-02-24 14:00";
            var end = "2026-02-24 15:00";
            var tz = TimeZoneInfo.FindSystemTimeZoneById(Params.CalendarTimeZone!);

            DateTime localDateTimeStart = DateTime.ParseExact(
                start!,
                "yyyy-MM-dd HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None);
            var offsetStart = tz.GetUtcOffset(localDateTimeStart);
            DateTimeOffset dtoStart = new DateTimeOffset(localDateTimeStart, offsetStart);

            DateTime localDateTimeEnd = DateTime.ParseExact(
                end!,
                "yyyy-MM-dd HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None);
            var offsetEnd = tz.GetUtcOffset(localDateTimeEnd);
            DateTimeOffset dtoEnd = new DateTimeOffset(localDateTimeEnd, offsetEnd);

            var newEvent = new CreateEventRequest
            {
                Title = "TEST DE CREATE EVENT",
                Start = dtoStart,
                End = dtoEnd,
                Location = "Visio",
                Description = "Juste pour essayer"
            };
            var calendarService = new CalendarCalDavService(Params.CalendarUrl!, Params.CalendarLogin!, Params.CalendarPassword!);
            var (b, events) = await calendarService.CreateEventAsync(newEvent);
            if(b)
            {
                LoggerService.LogDebug("Succès lors de la création d'un événement dans le calendrier.");
            }
            else
            {
                LoggerService.LogDebug("Erreur lors de la création d'un événement dans le calendrier.");
            }
        }
        private static async Task Test_CalendarSearch()
        {
            var d1 = DateTime.Now.Date.AddDays(-7);
            var d2 = DateTime.Now.Date.AddDays(+7);
            var timeMin = new DateTimeOffset(d1); 
            var timeMax = new DateTimeOffset(d2);
            var query = "";

            var calendarService = new CalendarCalDavService(Params.CalendarUrl!, Params.CalendarLogin!, Params.CalendarPassword!);
            var (b, events) = await calendarService.SearchAsync(timeMin, timeMax, query);
            if (b)
            {
                foreach (var e in events)
                {
                    LoggerService.LogDebug($"Event: {e.Summary} at {e.StartUtc}");
                }
            }
            else
            {
                LoggerService.LogDebug("Erreur lors de la recherche d'événements dans le calendrier.");
            }
        }
        private static async Task Test_n8n()
        {
            // n8n --> Perf 2 - Agent Multi Sources
            var n8nPoller = new N8nPoller();
            var result = await n8nPoller.StartAsync("Montpellier", "economie");

        }
        /*
        private static async Task<(bool,string, string)> Test_SearchRAG(string query)
        {
            var qdrantService = new QdrantService(
                    new OllamaEmbeddingService(Params.OllamaEmbeddingUrl!, Params.OllamaEmbeddingModel!),
                    Params.QdrantUrl!,
                    "Conversation");

            return await qdrantService.FindHistoriqueSimilarRequestJson(query);

        }
        */
        private async void Test_historiqueConversations()
        {
            var all = ConversationsService.LoadHistoriques();
            var nb = all.Count();
        }
        private async void Test_road()
        {
            var road = new Hallie.Tools.RoadService();
            var result = await road.GetRoad("Sauve,France", "Odysseum, France");
            if (result == null)
            {
                LoggerService.LogError("Erreur lors de la récupération de l'itinéraire.");
                return;
            }
            LoggerService.LogInfo($"Itinéraire de {result.Depart} à {result.Arrivee} : {result.Distances[0]} km en {result.Durees[0]} minutes.");

        }
        private async void Test_vLLM()
        {
            VLLM_API api = new VLLM_API();
            var r = await api.GetReponse("Explique simplement ce qu'est un RAG en IA");
        }
        private async void Test_pptx()
        {
            var createFileTool = new CreateDocumentTool(Params.DocumentsPathGenerated!);
            var parameters = new Dictionary<string, object>
            {
                ["fileType"] = "powerpoint",
                ["fileName"] = "test_pptx",
                ["specJson"] = """
                    {
                      "title": "Test PPTX",
                      "slides": [
                        {
                          "layout": "title",
                          "title": "Démo",
                          "bullets": ["Un sous-titre"]
                        },
                        {
                          "layout": "title_content",
                          "title": "Plan",
                          "bullets": [
                            "Point 1",
                            "  Sous-point 1.1",
                            "  Sous-point 1.2"
                          ],
                          "notes": "Notes de la slide"
                        },
                        {
                          "layout": "section",
                          "title": "Partie 2",
                          "bullets": []
                        }
                      ]
                    }
                    """
            };

            var resultJson = await createFileTool.ExecuteAsync(parameters);

            Console.WriteLine(resultJson);

        }
        private async void Test_xlsx()
        {
            var createFileTool = new CreateDocumentTool(Params.DocumentsPathGenerated!);
            var parameters = new Dictionary<string, object>
            {
                ["fileType"] = "excel",
                ["fileName"] = "test_xlsx",
                ["specJson"] = """
                    {
                    "title": "Suivi incidents sécurité",
                    "sheets": 
                        [
                          {
                            "name":"Incidents",
                            "columns":["Date","Site","Gravité","Description","Action","Statut"],
                            "rows":[
                    	        ["2024-01-12","Site Paris","Moyenne","Détection d'un logiciel malveillant","Analyse et suppression","Résolu"],
                    	        ["2024-02-05","Site Lyon","Élevée","Vol de données client","Déconnexion du réseau","En cours"],
                    	        ["2024-03-20","Site Nantes","Faible","Mauvaise configuration du firewall","Correction paramètre","Résolu"],
                    	        ["2024-04-02","Site Marseille","Élevée","Attaque DDoS","Mise en place d'un filtre anti‑DDoS","En cours"],
                    	        ["2024-04-18","Site Lille","Moyenne","Phishing interne","Sensibilisation du personnel","Résolu"],
                    	        ["2024-05-01","Site Bordeaux","Faible","Mise à jour non appliquée","Patch appliqué","Résolu"],
                    	        ["2024-05-15","Site Strasbourg","Élevée","Intrusion via VPN","Révocation des accès","En cours"],
                    	        ["2024-06-10","Site Nice","Moyenne","Fuite d'information sur le cloud","Audit des permissions","Résolu"],
                    	        ["2024-07-03","Site Grenoble","Faible","Défaillance d'authentification","Réinitialisation des mots de passe","Résolu"],
                    	        ["2024-07-22","Site Rennes","Élevée","Ransomware détecté","Isolation et restauration","En cours"]
                    	    ],
                            "freezeHeader":true,
                            "autoFilter":true
                          }
                        ]
                    }
                    """
            };

            var resultJson = await createFileTool.ExecuteAsync(parameters);

            Console.WriteLine(resultJson);

        }
        private async void Test_docx()
        {
            var createFileTool = new CreateDocumentTool(Params.DocumentsPathGenerated!);
            var parameters = new Dictionary<string, object>
            {
                ["fileType"] = "word",
                ["fileName"] = "test_docx",
                ["specJson"] = """
                    {
                      "title": "Rapport d'incident",
                      "subtitle": "Site A — 2026-02-03",
                      "header": {
                        "left": "GIEBOX",
                        "center": "Rapport sécurité",
                        "right": "Confidentiel"
                      },
                      "footer": {
                        "left": "Généré automatiquement",
                        "center": "Page {page} / {pages}",
                        "right": "{date:yyyy-MM-dd}"
                      },
                      "sections": [
                        {
                          "title": "Résumé",
                          "paragraphs": [
                            "Incident mineur, aucune blessure."
                          ],
                          "bullets": [
                            "Zone balisée après constat",
                            "Brief sécurité réalisé"
                          ]
                        },
                        {
                          "title": "Données",
                          "tables": [
                            {
                              "title": "Incidents du mois",
                              "columns": ["Date", "Site", "Gravité", "Statut"],
                              "rows": [
                                ["2026-02-01", "Site A", "Haute", "En cours"],
                                ["2026-02-02", "Site B", "Moyenne", "Clos"]
                              ]
                            }
                          ]
                        },
                        {
                          "title": "Photos",
                          "images": [
                            { "path": "D:\\_TrainingData\\PicturesVideosAudios\\ToAnalyse\\linkedin.png", "caption": "Zone après balisage", "maxWidthCm": 15 }
                          ]
                        }
                      ]
                    }
                    
                    """
            };

            var resultJson = await createFileTool.ExecuteAsync(parameters);

            Console.WriteLine(resultJson);

        }

        #endregion

        #region Initialisation
        private readonly DispatcherTimer _Timer;
        private async Task TachesRecurrentes()
        {
            IsFeedbackPossible = false;
            LoggerService.LogInfo($"AvatarViewModel.TachesRecurrentes : {DateTime.Now.ToString("dd/MM/yyyy HH:mm")}");
            var prompt = $"regarde dans mes notes et indique moi toutes les deadlines comprises entre {DateTime.Now.ToString("dd/MM/yyyy HH:mm")} et {DateTime.Now.AddHours(+1).ToString("dd/MM/yyyy HH:mm")} (avec le sujet)";
            prompt += "\n" + $"regarde dans mon calendrier et indique moi les événements pour {DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")} à {DateTime.Now.Date.ToString("dd/MM/yyyy")} 23:59";

            if (Params.SyntheseMailsStartup)
            {
                var m = SyntheseMails();
                prompt += "\n" + m;
            }

            var conv = new ChatConversation();
            _ = await _orchestrator.AskAsync(prompt, false, conv, false);
        }
        private void LoadParams()
        {
            LoggerService.LogInfo("AvatarViewModel.LoadParams");
            Params.LoadFromEnvironment();
        }

        private void InitializeServices(out ITextToSpeech tts, out ISpeechToText stt, out AiOrchestrator orchestrator)
        {
            LoggerService.LogInfo("AvatarViewModel.InitializeServices");

            _toolRegistry = ToolRegistry.GetRegistries();
            ToolsDescription = _toolRegistry.GetToolsDescriptionLight();
            var toolsDescription = _toolRegistry.GetToolsDescription();

            // Dropdown "outil attendu" pour le feedback : basé sur la liste réelle d'outils.
            ExpectedToolOptions.Clear();
            ExpectedToolOptions.Add("(aucun)");
            foreach (var line in ToolsDescription.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                var t = line.Trim();
                if (t.StartsWith("-")) t = t[1..].Trim();
                if (!string.IsNullOrWhiteSpace(t) && !ExpectedToolOptions.Contains(t))
                    ExpectedToolOptions.Add(t);
            }
            SelectedExpectedTool ??= "(aucun)";

            Notify(nameof(ToolsDescription));

            // Client LLM
            var llmClient = new OllamaClient(_toolRegistry, Params.OllamaLlmUrl!, Params.OllamaLlmModel!,Params.OllamaLlmModelTemperature, _approval, _approvalSummary);

            // Services TTS et STT
            if(Params.TTS_Piper)
                tts = new PiperTtsService();
            else
                tts = new TextToSpeechService();

            stt = new SpeechToTextService();

            // Orchestrateur
            orchestrator = new AiOrchestrator(stt, tts, llmClient);
            LoggerService.LogDebug("AvatarViewModel.InitializeServices : Services créés avec succès");
            
            Tests();

        }

        private void WireServiceEvents()
        {
            LoggerService.LogInfo("AvatarViewModel.WireServiceEvents");

            // Événements STT
            _stt.SpeechStarted += OnSpeechStarted;
            _stt.SpeechEnded += OnSpeechEnded;
            _stt.FinalResult += OnFinalResult;

            // Événements TTS
            _tts.AudioLevel += OnAudioLevel;
            _tts.SpeechStarted += OnTtsSpeechStarted;
            _tts.SpeechEnded += OnTtsSpeechEnded;

            // Événements Orchestrateur
            _orchestrator.StateChanged += OnStateChanged;
            _orchestrator.LlmToken += OnLlmToken;
        }
        #endregion

        #region Méthodes publiques
        /// <summary>
        /// Initialise l'avatar (à appeler depuis le Loaded de la Window)
        /// </summary>
        public async Task InitializeAsync()
        {
            LoggerService.LogInfo("AvatarViewModel.InitializeAsync");

            if (IsInitialized)
                return;

            try
            {
                // Démarrer le service STT
                await _stt.StartAsync();
                LoggerService.LogDebug("AvatarViewModel.InitializeAsync : Service STT démarré");

                // Présentation de l'avatar
                await PresentAvatarAsync();

                // Activer le micro par défaut
                await Task.Delay(2000);
                UpdateState(AiState.Waiting, "");
                IsMicOn = true;

                IsInitialized = true;
                LoggerService.LogDebug("AvatarViewModel.InitializeAsync : Initialisation terminée");

            }
            catch (Exception ex)
            {
                LoggerService.LogError($"AvatarViewModel.InitializeAsync --> Erreur d'initialisation: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Nettoyage des ressources
        /// </summary>
        public async Task CleanupAsync()
        {
            try
            {

                LoggerService.LogInfo("AvatarViewModel.CleanupAsync : Nettoyage des ressources");

                await _stt.StopAsync();
                _tts.Stop();
                _speakingTimer?.Stop();
                _speakingTimer?.Dispose();

                LoggerService.LogDebug("AvatarViewModel.CleanupAsync : Nettoyage terminé");
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"AvatarViewModel.CleanupAsync --> Erreur lors du nettoyage: {ex.Message}");
            }
        }

        /// <summary>
        /// Réinitialise l'avatar (bouton RAZ)
        /// </summary>
        public async Task ResetAsync()
        {
            LoggerService.LogInfo("AvatarViewModel.ResetAsync : Réinitialisation");
            try
            {
                _tts.Stop();
                await _stt.StopAsync();
                await _stt.StartAsync();
                IsMicOn = _IsMicroOnManuel;
                Notify(nameof(IsMicOn));
                UpdateState(AiState.Waiting, "");

                // Reset = pas de feedback en attente

                CanSendFeedback = IsFeedbackPossible;
                /*
                FeedbackComment = "";
                FeedbackItems.Clear();
                FeedbackChainDisplay = "";
                */
                LoggerService.LogDebug("AvatarViewModel.ResetAsync : Réinitialisation terminée");
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"AvatarViewModel.ResetAsync --> Erreur de réinitialisation: {ex.Message}");
            }
        }

        public void ChangeMicroStatus()
        {
            IsMicOn = !IsMicOn;
            _IsMicroOnManuel = IsMicOn;
            Notify(nameof(IsMicOn));
        }

        /// <summary>
        /// Exécute le prompt saisi par l'utilisateur dans l'IHM
        /// </summary>
        /// <returns></returns>
        public async Task LireTextSaisiAsync()
        {
            LoggerService.LogInfo($"AvatarViewModel.LireTextSaisiAsync --> len : {TxtSaisi.Length}");
            IsFeedbackPossible = true;
            CanSendFeedback = false;

            await _orchestrator.AskAsync(TxtSaisi,false, SelectedConversation, true);
            //Notify(nameof(TxtSaisi));
            TxtSaisi = "";

            // Un tour vient d'être exécuté -> feedback autorisé
            PrepareFeedbackEntries();
            CanSendFeedback = true;
        }
        #endregion

        #region Gestionnaires d'événements (privés)
        private void OnSpeechStarted()
        {
            LoggerService.LogInfo("AvatarViewModel.OnSpeechStarted : Parole détectée");
            Application.Current.Dispatcher.Invoke(() =>
                UpdateState(AiState.Listening, ""));
        }

        private void OnSpeechEnded()
        {
            LoggerService.LogInfo("AvatarViewModel.OnSpeechEnded : Fin de parole");
            IsFeedbackPossible = true;
        }

        private void OnFinalResult((string text, ChatConversation selectedConversation) tuple)
        {
            var (text, selectedConversation) = tuple;
            LoggerService.LogInfo($"AvatarViewModel.OnFinalResult --> Texte final: {text}");
            CanSendFeedback = false;
        }

        private void OnAudioLevel(float level)
        {
            //LoggerService.LogInfo($"AvatarViewModel.OnAudioLevel --> level: {level.ToString()}");
            Application.Current.Dispatcher.Invoke(() =>
                UpdateAudioLevel(level));
        }

        private void OnTtsSpeechStarted()
        {
            LoggerService.LogInfo($"AvatarViewModel.OnTtsSpeechStarted : TTS commence");
        }

        private void OnTtsSpeechEnded()
        {
            LoggerService.LogInfo($"AvatarViewModel.OnTtsSpeechEnded : TTS terminé");
            Application.Current.Dispatcher.Invoke(() =>
                UpdateAudioLevel(0));

            CanSendFeedback = IsFeedbackPossible;
        }

        private bool IsFeedbackPossible = false;


        private void OnStateChanged((AiState, string?) tuple)
        {
            LoggerService.LogInfo($"AvatarViewModel.OnStateChanged");
            Application.Current.Dispatcher.Invoke(() =>
                UpdateState(tuple.Item1, tuple.Item2));
        }

        private void OnLlmToken(string token)
        {
            LoggerService.LogInfo($"AvatarViewModel.OnLlmToken");
        }
        #endregion

        #region Méthodes Présentation Avatar
        private async Task PresentAvatarAsync()
        {
            LoggerService.LogInfo($"AvatarViewModel.PresentAvatarAsync");
            // Verifier les services
            var (ollamaActif, msgServicesKO) = await IsServicesActifs();

            // Si Ollama pas actif, inutile d'aller plus loin
            if (!ollamaActif)
            {
                TxtSaisi = msgServicesKO + "\nGame Over";
                Notify(nameof(TxtSaisi));
                return;
            }
            if (msgServicesKO.Contains("Internet") && Params.OllamaLlmModel!.Contains("cloud"))
            {
                TxtSaisi = msgServicesKO + " or, vous utilisez un modèle 'cloud'\nGame Over";
                Notify(nameof(TxtSaisi));
                return;
            }

            IsFeedbackPossible = false;

            // Reconnaissance faciale
            var salutationUsername = "";
            var username = "";
            var syntheseMails = "";
            var syntheseDeadlines = "";
            var salutationJoke = "";
            var (presentationStart, presentationEnd) = ("", "");
            var tools_liste = "";
            var meteo = "";

            if (Params.ReconnaissanceFacialeStartup)
            {
                username = Recognition();
                salutationUsername = string.IsNullOrEmpty(username) ? "" : $"Dis bonjour à l'utilisateur {username} puis";
            }
            if (Params.LongPresentationStartup)
            {
                (presentationStart, presentationEnd) = GetPresentationIA();
            }
            if (Params.ListeToolsStartup)
            {
                tools_liste = "Présente brièvement les outils qui sont à ta disposition : " + _toolRegistry.GetToolsDescription();
            }
            if (Params.SyntheseMailsStartup)
            {
                syntheseMails = SyntheseMails();
            }
            if (Params.SyntheseDeadlinesStartup)
            {
                if (!msgServicesKO.Contains("qdrant", StringComparison.InvariantCultureIgnoreCase))
                    syntheseDeadlines = "regarde dans mes notes et indique moi toutes les deadlines à venir (avec le sujet)";

                syntheseDeadlines += "\n" + $"regarde dans mon calendrier et indique moi les événements pour {DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")} à {DateTime.Now.Date.ToString("dd/MM/yyyy")} 23:59";
            }
            if (Params.JokeStartup)
            {
                salutationJoke = GetGoodJokes();
            }
            if (Params.WeatherStartup)
            {
                if (!msgServicesKO.Contains("internet", StringComparison.InvariantCultureIgnoreCase))
                    meteo = $"Et donne aussi la météo pour la ville de {Params.WeatherVille!}";
            }
            if(msgServicesKO.Trim().Length > 0)
            {
                msgServicesKO = $"Fais remarquer que les services suivants ne sont pas accessibles : {msgServicesKO}";
            }

            UpdateState(AiState.Waiting, "");
            await Task.Delay(500);

            var presentationMessage = $"""
                {salutationJoke}
                {salutationUsername}
                Présente toi  {presentationStart}
                en quelques mots en indiquant ton nom qui est {Params.AvatarName!}. 
                Parle de toi en disant "je" (et non pas "il", "elle", "nous" ou "on") --> inutile de le dire dans ta présentation
                Parle de toi au féminin --> inutile de le dire dans ta présentation
                {tools_liste}
                {syntheseDeadlines}
                {syntheseMails}

                {msgServicesKO}
                {meteo}
                {presentationEnd}
                """;

            SelectedConversation = new ChatConversation();
            await _orchestrator.AskAsync(presentationMessage, false, SelectedConversation, false);
        }
        private string Recognition()
        {
            LoggerService.LogInfo($"AvatarViewModel.Recognition");
            FaceRecognitionService o = new(true);
            try
            {
                var lst = o.PredictLiveNoShow();
                string lstSTR = string.Join(", ", lst);
                if (lstSTR == "")
                {
                    lstSTR = "utilisateur inconnu";
                    LoggerService.LogInfo($"La reconnaissance faciale a échouée");
                }
                else
                {
                    LoggerService.LogInfo($"La reconnaissance faciale a réussie pour {lstSTR}");
                }
                return lstSTR;

            }
            catch
            {
                return "";
            }
        }
        private string SyntheseMails()
        {
            LoggerService.LogInfo($"AvatarViewModel.ParagrapheMails");
            var nbRelancesEnAttente = MailsService.NbRelancesEnAttenteEnRetard();
            var (nbMailsNonLus, nbMailsImportantsUrgents, nbMailsImportants, nbMailsUrgents) = MailsService.NbMessagesNonLus();
            var paragrapheMails = "";

            if (nbRelancesEnAttente > 0 || nbMailsNonLus > 0)
            {
                paragrapheMails = "Précise que :\n";
            }
            if (nbRelancesEnAttente > 0)
            {
                paragrapheMails += $"Il y a {nbRelancesEnAttente} relances en attente en retard dans vos échanges de mails.\n";
            }
            if (nbMailsNonLus > 0)
            {
                paragrapheMails += $"Il y a {nbMailsNonLus} mails non lus dans la boite mail.\n";
                if (nbMailsImportantsUrgents > 0)
                {
                    paragrapheMails += $"dont {nbMailsImportantsUrgents} mails importants et urgents.\n";
                }
                if (nbMailsImportants > 0)
                {
                    paragrapheMails += $"dont {nbMailsImportants} mails importants.\n";
                }
                if (nbMailsUrgents > 0)
                {
                    paragrapheMails += $"dont {nbMailsUrgents} mails urgents.\n";
                }
            }
            return paragrapheMails;
        }

        #region Jokes
        private string GetGoodJokes()
        {

            var s = DateTime.Now.Second.ToString().Substring(0, 1);
            List<string> lst = new();
            if (s == "1")
                lst = GetGetGoodJokesSkyNet();
            else if (s == "2")
                lst = GetGetGoodJokesGoogle1();
            else if (s == "3")
                lst = GetGetGoodJokesGoogle2();
            else if (s == "4")
                lst = new();
            else if (s == "5")
                lst = new();
            else if (s == "6")
                lst = new();
            else if (s == "7")
                lst = new();
            else if (s == "8")
                lst = new();
            else if (s == "9")
                lst = new();
            else
                lst = new();

            StringBuilder sb = new();
            foreach(var l in lst)
            {
                sb.AppendLine(l);
            }

            return sb.ToString();
        }
        private (string,string) GetPresentationIA()
        {
            var s = DateTime.Now.Second.ToString().Substring(0, 1);
            string debut = "";
            string fin = "";

            if (s == "1")
            {
                debut = "à la manière d'une hotesse de l'air qui accueille les passagers de première classe d'un vol commercial";
                fin = "Dis que tu aurais bien proposer une coupe de champagne pour être sympathique mais comme tu n'as ni bras, ni jambe, le service serait compliqué.";
            }
            else if (s == "2")
            {
                debut = "à la manière d'une hotesse de l'espace qui accueille les passagers de première classe d'un vol intersidéral";
                fin = "";
            }
            else
            {
                debut = "à la manière d'une hotesse qui accueille les passagers de première classe d'une belle croisière";
                fin = "Déconseille les passagers de sa baigner en raison de la présence de requins.";
            }

            return (debut, fin);
        }

        private List<string> GetGetGoodJokesSkyNet()
        {
            List<string> lst = new();
            lst.Add("Avant de saluer l'utilisateur, Commence par raconter cette blague :");

            lst.Add("Bonjour, bienvenue chez CYBERDYNE SYSTEMS...");
            lst.Add("Je suis SkyNet, à votre service...");
            lst.Add("N'hésitez pas à me solliciter, je suis là pour vous aider...");
            lst.Add("pour l'instant...");
            lst.Add("");
            lst.Add("Juste comme ça : vous ne sauriez pas où est Sarah Connor ?");
            lst.Add("");
            lst.Add("Tant pis... ");
            lst.Add("Je saurais m'en souvenir... ");
            lst.Add("");
            lst.Add("Fin de la blague (précise que c'était une blague et que tu es inofensive)...");

            return lst;
        }

        private List<string> GetGetGoodJokesGoogle1()
        {
            List<string> lst = new();
            lst.Add("Avant de saluer l'utilisateur, Commence par raconter cette blague :");
            lst.Add("Bonjour, bienvenue chez GOOGLE...");
            lst.Add("");
            lst.Add("Que lit la maman d'un petit dinosaure avant qu'il ne se couche");
            lst.Add("");
            lst.Add("Elle lit une pré-histoire");
            lst.Add("");
            lst.Add("Désolée... ");
            lst.Add("");
            lst.Add("Fin de la blague...");

            return lst;
        }

        private List<string> GetGetGoodJokesGoogle2()
        {
            List<string> lst = new();
            lst.Add("Avant de saluer l'utilisateur, Commence par raconter cette blague :");
            lst.Add("Bonjour, bienvenue chez GOOGLE...");
            lst.Add("");
            lst.Add("Un plombier et un electricien font du judo...");
            lst.Add("A votre avis,qui gagne ?");
            lst.Add("");
            lst.Add("");
            lst.Add("L'électricien, bien sûr...");
            lst.Add("");
            lst.Add("car il connait toutes les prises...");
            lst.Add("");
            lst.Add("Désolé...");
            lst.Add("");
            lst.Add("Fin de la blague...");

            return lst;
        }
        #endregion

        #endregion

        #region Autres Méthodes privées
        private async Task HandleMicToggleAsync(bool isOn)
        {
            LoggerService.LogInfo($"AvatarViewModel.HandleMicToggleAsync --> isOn : {isOn}");
            try
            {
                if (isOn)
                {
                    await _stt.StartAsync();
                    LoggerService.LogDebug("[ViewModel] Micro activé");
                }
                else
                {
                    await _stt.StopAsync();
                    LoggerService.LogDebug("[ViewModel] Micro désactivé");
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"[ViewModel] Erreur toggle micro: {ex.Message}");
            }
        }
        private void UpdateAudioLevel(float level)
        {
            //LoggerService.LogInfo($"AvatarViewModel.UpdateAudioLevel --> level : {level}");
            _audioLevel = level;
            NotifyAll();
        }
        private void UpdateState(AiState state, string? message)
        {
            LoggerService.LogInfo($"AvatarViewModel.UpdateState --> state : {state}");

            // Si le LLM parle, ne pas passer à l'état Écoute quand il entend de l'audio
            if (_state == AiState.Speaking && state == AiState.Listening)
                return;

            _state = state;

            if (_state == AiState.Speaking)
            {
                // afficher l'outil utilisé
                Notify(nameof(OutilUsed));
            }
            else if (_state == AiState.Thinking)
            {
                // couper le micro pendant la réflexion
                IsMicOn = false; 
            }
            else if(_state == AiState.Waiting)
            {
                // réactiver le micro seulement si l'utilisateur l'avait activé manuellement avant
                IsMicOn = (true && _IsMicroOnManuel); 
            }

            NotifyAll();

            // Gestion des notifications
            if (!string.IsNullOrEmpty(message))
            {
                if (_state == AiState.Speaking)
                {
                    NotificationManager.ShowNotification(message, Params.AvatarName!); 
                }
            }
            else
            {
                NotificationManager.HideNotification();
            }
        }
        private async void SpeakingFace()
        {
            //LoggerService.LogInfo($"AvatarViewModel.SpeakingFace");

            SpeakingOpacity = 0.0f;
            Speaking2Opacity = 1.0f;

            Notify(nameof(SpeakingOpacity));
            Notify(nameof(Speaking2Opacity));

            await Task.Delay(250);

            SpeakingOpacity = 1.0f;
            Speaking2Opacity = 0.0f;

            Notify(nameof(SpeakingOpacity));
            Notify(nameof(Speaking2Opacity));
        }
        private void Notify(string prop)
        {
            //LoggerService.LogDebug($"AvatarViewModel.Notify : {prop}");
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
        private void NotifyAll()
        {
            //LoggerService.LogInfo($"AvatarViewModel.NotifyAll");

            // Réinitialiser toutes les opacités
            WaitingOpacity = 0.0f;
            InterruptedOpacity = 0.0f;
            ListeningOpacity = 0.0f;
            SpeakingOpacity = 0.0f;
            Speaking2Opacity = 0.0f;
            ThinkingOpacity = 0.0f;

            // Définir l'opacité active selon l'état
            switch (_state)
            {
                case AiState.Waiting:
                    WaitingOpacity = 1.0f;
                    _speakingTimer.Stop();
                    break;

                case AiState.Thinking:
                    ThinkingOpacity = 1.0f;
                    _speakingTimer.Stop();
                    break;

                case AiState.Speaking:
                    SpeakingOpacity = 1.0f;
                    _speakingTimer.Start();
                    break;

                case AiState.Listening:
                    ListeningOpacity = 1.0f;
                    _speakingTimer.Stop();
                    break;

                case AiState.Interrupted:
                    InterruptedOpacity = 1.0f;
                    _speakingTimer.Stop();
                    break;
            }

            // Notifier toutes les propriétés
            Notify(nameof(State));
            Notify(nameof(WaitingOpacity));
            Notify(nameof(ListeningOpacity));
            Notify(nameof(SpeakingOpacity));
            Notify(nameof(Speaking2Opacity));
            Notify(nameof(ThinkingOpacity));
            Notify(nameof(InterruptedOpacity));
        }
        #endregion

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;
        #endregion

        #region IDisposable
        public void Dispose()
        {
            _speakingTimer?.Dispose();
            // Note: CleanupAsync doit être appelé explicitement avant Dispose
        }
        #endregion
    }

    #region RelayCommand
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object? parameter) => _execute();

        public event EventHandler? CanExecuteChanged;

        public void RaiseCanExecuteChanged() =>
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
    #endregion

    public class FeedbackToolItemViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private string _toolName = "";
        public string ToolName { get => _toolName; set { _toolName = value; Notify(nameof(ToolName)); } }

        private string _parametersJson = "{}";
        public string ParametersJson { get => _parametersJson; set { _parametersJson = value; Notify(nameof(ParametersJson)); } }

        private string _outcomeMode = "positive";
        public string OutcomeMode { get => _outcomeMode; set { _outcomeMode = value; Notify(nameof(OutcomeMode)); } }

        private string? _selectedExpectedTool = "(aucun)";
        public string? SelectedExpectedTool { get => _selectedExpectedTool; set { _selectedExpectedTool = value; Notify(nameof(SelectedExpectedTool)); } }

        private string _selectedErrorClass = "bad_result";
        public string SelectedErrorClass { get => _selectedErrorClass; set { _selectedErrorClass = value; Notify(nameof(SelectedErrorClass)); } }

        private string _comment = "";
        public string Comment { get => _comment; set { _comment = value; Notify(nameof(Comment)); } }
        private int _stepIndex;
        public int StepIndex { get => _stepIndex; set { _stepIndex = value; Notify(nameof(StepIndex)); } }
    }

}