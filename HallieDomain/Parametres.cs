using DotNetEnv;

namespace HallieDomain
{
    public class TypeConnexionString
    {
        public string BddName { get; set; } = "";
        public string ConnexionString { get; set; } = "";
    }
    public static class Params
    {
        #region Propriétés 
        public static string? AvatarName { get; set; }
        public static string? BddHallieConnexionString { get; set; } 

        //DELAI_VERIF_AUTO_DEADLINE : si 0, pas de vérif auto
        public static int? DelaiVerifAutoDeadlines { get; set; } = 0;
        public static bool TTS_Piper { get; set; }
        public static string? TTS_Piper_Voice { get; set; }

        public static bool ReconnaissanceFacialeStartup { get; set; }
        public static bool LongPresentationStartup { get; set; }
        public static bool ListeToolsStartup { get; set; }
        public static bool SyntheseMailsStartup { get; set; }
        public static bool SyntheseDeadlinesStartup { get; set; }
        public static bool JokeStartup { get; set; }
        public static bool HistoriqueLlmHelper { get; set; }
        public static string? NomPrenom { get; set; }
        public static string? RoleEntreprise { get; set; }
        public static string? EntrepriseNom { get; set; }
        public static string? WeatherApiKey { get; set; }
        public static string? WeatherUrl { get; set; }
        public static string? WeatherVille { get; set; }
        public static bool WeatherStartup { get; set; }

        public static string? SmtpUser { get; set; }
        public static string? SmtpPass { get; set; }
        public static string? SmtpHost { get; set; }
        public static int SmtpPort { get; set; }
        public static string? ImapHost { get; set; }
        public static int ImapPort { get; set; }
        public static string? JsonMailsNonLus { get; set; }
        public static string? JsonMailsSuivi { get; set; }
        public static List<TypeConnexionString> BddConnexionsString { get; set; } = [];

        public static string? OllamaLlmUrl { get; set; }
        public static string? OllamaLlmModel { get; set; }
        public static double OllamaLlmModelTemperature { get; set; }
        public static string? OllamaLlmModelVision { get; set; }
        public static string? MultimediaPathToAnalyse { get; set; }
        public static string? MultimediaPathAnalysed { get; set; }


        public static string? DocumentsPathToVectorize { get; set; }
        public static string? DocumentsPathVectorized { get; set; }
        public static string? DocumentsPathGenerated { get; set; } 


        public static string? OllamaEmbeddingUrl { get; set; }
        public static string? OllamaEmbeddingModel { get; set; }

        public static string? WHisperModele { get; set; }

        public static string? QdrantUrl { get; set; }
        public static string? QdrantDashboardUrl { get; set; }
        public static string? QdrantCollectionsName { get; set; }

        public static string? CommandesWindows_WhiteList { get; set; }

        public static string? WebSearchUrl { get; set; }
        public static string? WebSearchApiKey { get; set; }
        public static int BoucleIterationMax { get; set; }

        public static string? CalendarUrl { get; set; }
        public static string? CalendarTimeZone { get; set; }
        public static string? CalendarLogin { get; set; }
        public static string? CalendarPassword { get; set; }

        public static bool LlmExternalToUse { get; set; }
        public static string? LlmExternalApi { get; set; }
        public static string? LlmExternalUrl { get; set; }
        public static string? LlmExternalModel { get; set; }
        public static double LlmExternalTemperature { get; set; }


        #endregion

        #region Méthode publique
        public static void LoadFromEnvironment()
        {
            Env.Load(); // Charge automatiquement le fichier .env

            AvatarName = Environment.GetEnvironmentVariable("AVATAR_NAME");
            BddHallieConnexionString = Environment.GetEnvironmentVariable("BDD_HALLIE_CONNEXION_STRING");

            DelaiVerifAutoDeadlines = int.Parse(Environment.GetEnvironmentVariable("DELAI_VERIF_AUTO_DEADLINE") ?? "0"); 

            TTS_Piper = Environment.GetEnvironmentVariable("TTS_PIPER") == "1" ? true : false;
            TTS_Piper_Voice = Environment.GetEnvironmentVariable("TTS_PIPER_VOICE");

            ReconnaissanceFacialeStartup = Environment.GetEnvironmentVariable("RECONNAISSANCE_FACIALE_STARTUP") == "1" ? true : false;
            LongPresentationStartup = Environment.GetEnvironmentVariable("LONG_PRESENTATION_STARTUP") == "1" ? true : false;
            ListeToolsStartup = Environment.GetEnvironmentVariable("LISTE_TOOLS_STARTUP") == "1" ? true : false;
            SyntheseMailsStartup = Environment.GetEnvironmentVariable("SYNTHESE_MAIL_STARTUP") == "1" ? true : false;
            SyntheseDeadlinesStartup = Environment.GetEnvironmentVariable("SYNTHESE_DEADLINE_STARTUP") == "1" ? true : false;

            JokeStartup = Environment.GetEnvironmentVariable("JOKE_STARTUP") == "1" ? true : false;

            HistoriqueLlmHelper = Environment.GetEnvironmentVariable("HISTORIQUE_LLM_HELPER") == "1" ? true : false;

            NomPrenom = Environment.GetEnvironmentVariable("NOM_PRENOM");
            RoleEntreprise = Environment.GetEnvironmentVariable("ROLE_ENTREPRISE");
            EntrepriseNom = Environment.GetEnvironmentVariable("ENTREPRISE_NOM");

            WeatherApiKey = Environment.GetEnvironmentVariable("WEATHER_API_KEY");
            WeatherUrl = "https://api.openweathermap.org/data/2.5/";
            WeatherStartup = Environment.GetEnvironmentVariable("WEATHER_STARTUP") == "1" ? true : false;
            WeatherVille = Environment.GetEnvironmentVariable("WEATHER_VILLE");

            SmtpUser = Environment.GetEnvironmentVariable("SMTP_USER");
            SmtpPass = Environment.GetEnvironmentVariable("SMTP_PASS");
            SmtpHost = Environment.GetEnvironmentVariable("SMTP_HOST");
            SmtpPort = int.Parse(Environment.GetEnvironmentVariable("SMTP_PORT") ?? "587");
            ImapHost = Environment.GetEnvironmentVariable("IMAP_HOST");
            ImapPort = int.Parse(Environment.GetEnvironmentVariable("IMAP_PORT") ?? "993");
            JsonMailsNonLus= Environment.GetEnvironmentVariable("MAILS_NON_LUS");
            JsonMailsSuivi = Environment.GetEnvironmentVariable("MAILS_SUIVI");

            OllamaLlmUrl = Environment.GetEnvironmentVariable("OLLAMA_LLM_URL");
            OllamaLlmModel = Environment.GetEnvironmentVariable("OLLAMA_LLM_MODEL");
            OllamaLlmModelTemperature = double.Parse(Environment.GetEnvironmentVariable("OLLAMA_LLM_MODEL_TEMPERATURE") ?? "1");
            OllamaLlmModelVision = Environment.GetEnvironmentVariable("OLLAMA_LLM_MODEL_VISION");

            MultimediaPathToAnalyse = Environment.GetEnvironmentVariable("MULTIMDEDIA_PATH_TO_ANALYSE");
            MultimediaPathAnalysed = Environment.GetEnvironmentVariable("MULTIMDEDIA_PATH_ANALYSED");

            DocumentsPathToVectorize = Environment.GetEnvironmentVariable("DOCUMENTS_PATH_TO_VECTORIZE");
            DocumentsPathVectorized = Environment.GetEnvironmentVariable("DOCUMENTS_PATH_VECTORIZED");
            DocumentsPathGenerated = Environment.GetEnvironmentVariable("DOCUMENTS_PATH_GENERATED");

            OllamaEmbeddingUrl = Environment.GetEnvironmentVariable("OLLAMA_EMBEDDING_URL");
            OllamaEmbeddingModel = Environment.GetEnvironmentVariable("OLLAMA_EMBEDDING_MODEL");

            WHisperModele = Environment.GetEnvironmentVariable("WHISPER_MODELE");

            QdrantUrl = Environment.GetEnvironmentVariable("QDRANT_URL");
            QdrantDashboardUrl = Environment.GetEnvironmentVariable("QDRANT_DASHBOARD_URL");
            QdrantCollectionsName = Environment.GetEnvironmentVariable("QDRANT_COLLECTIONS_NAME");

            CommandesWindows_WhiteList = Environment.GetEnvironmentVariable("COMMANDS_WINDOWS_WHITELIST");

            WebSearchUrl = Environment.GetEnvironmentVariable("WEBSEARCH_API_URL"); ;
            WebSearchApiKey = Environment.GetEnvironmentVariable("WEBSEARCH_API_KEY");

            BoucleIterationMax = int.Parse(Environment.GetEnvironmentVariable("BOUCLE_ITERATION_MAX") ?? "8");

            CalendarUrl = Environment.GetEnvironmentVariable("CALENDAR_URL");
            CalendarTimeZone = Environment.GetEnvironmentVariable("CALENDAR_TIME_ZONE");
            CalendarLogin = Environment.GetEnvironmentVariable("CALENDAR_LOGIN");
            CalendarPassword = Environment.GetEnvironmentVariable("CALENDAR_PASSWORD");

            LlmExternalToUse = Environment.GetEnvironmentVariable("LLM_EXTERNAL_TO_USE") == "1" ? true : false;
            LlmExternalApi = Environment.GetEnvironmentVariable("LLM_EXTERNAL_API");
            LlmExternalUrl = Environment.GetEnvironmentVariable("LLM_EXTERNAL_URL");
            LlmExternalModel = Environment.GetEnvironmentVariable("LLM_EXTERNAL_MODEL");
            LlmExternalTemperature = double.Parse(Environment.GetEnvironmentVariable("LLM_EXTERNAL_TEMPERATURE") ?? "1");

            var bddNames = Environment.GetEnvironmentVariable("BDD_NAMES");
            var bddConnexionsString = Environment.GetEnvironmentVariable("BDD_CONNEXIONS_STRING");

            var lstBddNames = bddNames?.Split(',').ToList() ?? [];
            var lstBddConnexionsString = bddConnexionsString?.Split(',').ToList() ?? [];

            if (lstBddNames.Count > 0 && lstBddConnexionsString.Count == lstBddNames.Count)
            {
                BddConnexionsString = [];
                for (int i = 0; i < lstBddNames.Count; i++)
                {
                    var typeConnexion = new TypeConnexionString
                    {
                        //BDD = Enum.Parse<BddName>(lstBddNames[i]),
                        BddName = lstBddNames[i],
                        ConnexionString = lstBddConnexionsString[i]
                    };
                    BddConnexionsString.Add(typeConnexion);
                }
            }
        }
        #endregion
    }
}

