using ExternalServices;
using System.Diagnostics;
using HallieDomain;

namespace Hallie.Tools
{

    #region Tool
    public class CommandsWindowsTool : ITool
    {
        public string Name => "commands_windows";
        public string Description => "Outil pour exécuter des commandes Windows spécifiques.";

        private CommandsWindowsService _Service;

        public CommandsWindowsTool()
        {
            LoggerService.LogInfo("CommandsWindowsTool");
            _Service = new CommandsWindowsService();
        }

        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            LoggerService.LogInfo("CommandsWindowsTool.ExecuteAsync");

            await Task.Delay(1); // Simule une opération asynchrone

            try
            {
                var appName = parameters["commande"].ToString();

                if (appName == null)
                    return "Commande Windows introuvable.";

                var bOk = _Service.ExecuteCommand(appName);
                if (!bOk)
                {
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        reponse = "",
                        error = "Échec de l'exécution de la commande Windows."
                    });
                }

                return JsonService.Serialize(new
                {
                    ok = true,
                    reponse = "Commande Windows exécutée.",
                    error = ""
                });

            }

            catch(Exception ex)
            {
                return JsonService.Serialize(new
                {
                    ok = false,
                    reponse = "",
                    error = ex.Message
                });
            }
        }

        public ToolParameter[] GetParameters()
        {
            return new[]
            {
                new ToolParameter
                {
                    Name = "commande",
                    Type = "string",
                    Description = "Le nom de l'application Windows qui sera exécutée",
                    Required = true
                }
            };
        }
    }
    #endregion

    #region Service
    public class CommandsWindowsService
    {
        public bool ExecuteCommand(string appName)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = appName,
                    UseShellExecute = true
                });
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
    #endregion
}
