using System.Text;

namespace ExternalServices
{


    public static class LoggerService
    {
        #region Variables
        private static int NiveauEcriture = 0;
        private static string NomFichierData = "paramsLogs.txt";
        #endregion

        #region Méthodes publiques
        public static void PurgeLogs()
        {
            LogInfo("LoggerService.PurgeLogs");

            var dateMax = DateTime.Now.AddDays(-14);
            var files = Directory.GetFiles(FichiersInternesService.DossierLogs);

            foreach (var file in files)
            {
                try
                {
                    var filename = Path.GetFileName(file);

                    var year = int.Parse(filename.Substring(4, 4));
                    var month = int.Parse(filename.Substring(9, 2));
                    var day = int.Parse(filename.Substring(12, 2));
                    var date = new DateTime(year, month, day);

                    if (date <= dateMax)
                        File.Delete(file);
                }
                catch (Exception ex)
                {
                    LogError($"LoggerService.PurgeLogs : {ex.Message}");
                }
            }
        }

        public static void LogError(string message)
        {
            if (NiveauEcriture <= 3)
                Log($"ERROR", message);
        }

        public static void LogWarning(string message)
        {
            if (NiveauEcriture <= 2)
                Log($"WARNING", message);
        }

        public static void LogInfo(string message)
        {
            if (NiveauEcriture <= 1)
                Log($"INFO", message);
        }

        public static void LogDebug(string message)
        {
            if (NiveauEcriture <= 0)
                Log($"DEBUG", message);
        }

        public static string GetLogDay(DateTime date)
        {
            var directory = FichiersInternesService.DossierLogs;
            var FullnameFichierLog = System.IO.Path.Combine(directory, Filename(date));
            var log = "";
            if (File.Exists(FullnameFichierLog))
                log = TxtService.ExtractTextFromTxt(FullnameFichierLog);
            return log;
        }

        public static int LoadParametres()
        {
            try
            {
                if (File.Exists(NomFichierData))
                {

                    string[] lignes = File.ReadAllLines(NomFichierData);

                    if (lignes.Length > 0)
                        NiveauEcriture = int.Parse(lignes[0]);

                }
                return NiveauEcriture;

            }

            catch (Exception ex)
            {
                LogError($"Erreur lors du chargement des paramètres de niveau d'écriture : {ex.Message}");
                NiveauEcriture = 0; // Par défaut, on remet à 0 en cas d'erreur
                return NiveauEcriture;
            }
        }

        public static bool SaveParametres(int selectedItem)
        {
            try
            {
                StringBuilder sb = new();
                sb.AppendLine(selectedItem.ToString());

                File.WriteAllText(NomFichierData, sb.ToString());
                NiveauEcriture = selectedItem;
                return true;
            }

            catch (Exception ex)
            {
                LogError($"Erreur lors de la sauvegarde des paramètres de niveau d'écriture : {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Méthodes privées
        private static void Log(string level, string message)
        {
            try
            {
                var directory = FichiersInternesService.DossierLogs;
                var FullnameFichierLog = System.IO.Path.Combine(directory, Filename());
                if (!System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                var ligneAdd = $"[{level}]\t[{GetCurrentTime()}]\t{message}";
                File.AppendAllText(FullnameFichierLog, ligneAdd + Environment.NewLine);
            }
            catch
            {

            }
        }

        private static string GetCurrentTime()
        {
            return DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
        }

        private static string Filename(DateTime? date = null)
        {
            DateTime dateOnly = DateTime.Now;
            if (date.HasValue)
                dateOnly = date.Value;

            return $"LOG_{dateOnly.ToString("yyyy-MM-dd")}.log";
        }
        #endregion
    }
}
