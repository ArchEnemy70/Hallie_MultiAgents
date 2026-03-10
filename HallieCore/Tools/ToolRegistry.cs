using HallieCore.Tools;
using HallieDomain;
using System.Text;

namespace Hallie.Tools
{
    public class ToolRegistry
    {
        private readonly Dictionary<string, ITool> _tools = new();

        public void Register(ITool tool)
        {
            _tools[tool.Name] = tool;
        }

        public ITool? GetTool(string name)
        {
            return _tools.ContainsKey(name) ? _tools[name] : null;
        }

        public async Task<string> ExecuteToolAsync(string toolName, Dictionary<string, object> parameters)
        {
            var tool = GetTool(toolName);
            if (tool == null)
                return $"Erreur : Outil '{toolName}' inconnu";

            try
            {
                return await tool.ExecuteAsync(parameters);
            }
            catch (Exception ex)
            {
                return $"Erreur lors de l'exécution : {ex.Message}";
            }
        }

        // Génère la description de tous les outils pour le LLM
        public string GetToolsDescription()
        {
            var descriptions = new StringBuilder();
            descriptions.AppendLine("Tu as accès aux outils suivants :");
            descriptions.AppendLine();

            foreach (var tool in _tools.Values)
            {
                descriptions.AppendLine($"- {tool.Name}: {tool.Description}");
                descriptions.AppendLine("  Paramètres:");

                foreach (var param in tool.GetParameters())
                {
                    var req = param.Required ? "requis" : "optionnel";
                    descriptions.AppendLine($"    * {param.Name} ({param.Type}, {req}): {param.Description}");
                }
                descriptions.AppendLine();
            }

            return descriptions.ToString();
        }

        public string GetToolsDescriptionLight()
        {
            var descriptions = new StringBuilder();

            foreach (var tool in _tools.Values)
            {
                //descriptions.AppendLine($"- {tool.Name}: {tool.Description}");
                descriptions.AppendLine($"- {tool.Name}");
            }

            return descriptions.ToString();
        }

        public static ToolRegistry GetRegistries()
        {
            var toolRegistry = new ToolRegistry();
            var ragSearchTool = new SearchDocumentTool(Params.OllamaEmbeddingUrl!, Params.OllamaEmbeddingModel!, Params.QdrantUrl!, Params.QdrantCollectionsName!);
            var ragIngestTool = new IngestDocumentTool(Params.OllamaEmbeddingUrl!, Params.OllamaEmbeddingModel!, Params.QdrantUrl!, Params.QdrantCollectionsName!);
            var weatherTool = new WeatherTool(Params.WeatherUrl!, Params.WeatherApiKey!);
            var sqlQueryTool = new SqlQueryTool(Params.BddConnexionsString!);
            var sqlActionTool = new SqlActionTool(Params.BddConnexionsString!);
            var commandsWindowsTool = new CommandsWindowsTool();
            var roadTool = new RoadTool();
            var webSearchTool = new WebSearchTool(Params.WebSearchUrl!, Params.WebSearchApiKey!);
            var webFetchTool = new WebFetchTool();
            var sendMailTool = new MailSendTool();
            var suiviMailTool = new MailSuiviTool();
            var picturesAnalyseTool = new PicturesAnalyseTool();
            var transcribTool = new TranscribTool();
            var createFileTool = new CreateDocumentTool(Params.DocumentsPathGenerated!);
            var extractDocTool = new ExtractFileTool();
            var extractZipTool = new ExtractZipTool();
            var openDocTool = new OpenFileTool();
            var deleteFileTool = new DeleteFileTool();
            var renameFileTool = new RenameFileTool();
            var copyFileTool = new CopyFileTool();
            var listeFilesTool = new ListeFilesTool();
            var findFileTool = new FindFileTool();
            var calendarSearchTool = new CalendarSearchTool(Params.CalendarUrl!, Params.CalendarLogin!, Params.CalendarPassword!, Params.CalendarTimeZone!);
            var calendarCreateTool = new CalendarCreateTool(Params.CalendarUrl!, Params.CalendarLogin!, Params.CalendarPassword!, Params.CalendarTimeZone!);
            var calendarDeleteTool = new CalendarDeleteTool(Params.CalendarUrl!, Params.CalendarLogin!, Params.CalendarPassword!, Params.CalendarTimeZone!);
            var pressReviewTool = new PressReviewTool();

            toolRegistry.Register(sqlQueryTool);
            toolRegistry.Register(sqlActionTool);
            toolRegistry.Register(ragSearchTool);
            toolRegistry.Register(ragIngestTool);
            toolRegistry.Register(weatherTool);
            toolRegistry.Register(commandsWindowsTool);
            toolRegistry.Register(roadTool);
            toolRegistry.Register(webSearchTool);
            toolRegistry.Register(webFetchTool);
            toolRegistry.Register(sendMailTool);
            toolRegistry.Register(suiviMailTool);
            toolRegistry.Register(picturesAnalyseTool);
            toolRegistry.Register(transcribTool);
            toolRegistry.Register(createFileTool);
            toolRegistry.Register(extractDocTool);
            toolRegistry.Register(extractZipTool);
            toolRegistry.Register(openDocTool);
            toolRegistry.Register(deleteFileTool);
            toolRegistry.Register(renameFileTool);
            toolRegistry.Register(copyFileTool);
            toolRegistry.Register(listeFilesTool);
            toolRegistry.Register(findFileTool);
            toolRegistry.Register(calendarSearchTool);
            toolRegistry.Register(calendarCreateTool);
            toolRegistry.Register(calendarDeleteTool);
            toolRegistry.Register(pressReviewTool);

            return toolRegistry;

        }
    }
}
