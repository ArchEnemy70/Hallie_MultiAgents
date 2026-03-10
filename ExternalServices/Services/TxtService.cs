using System.Text;

namespace ExternalServices
{
    public static class TxtService
    {
        public static string ExtractTextFromTxt(string filePath)
        {
            LoggerService.LogInfo($"TxtService.ExtractTextFromTxt : {filePath}");

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                LoggerService.LogInfo($"Le fichier n'existe pas : {filePath}");
                return "";
            }
            if (!IsTextFile(filePath))
            {
                LoggerService.LogInfo($"Le fichier est illisible : {filePath}");
                return "";
            }
            return File.ReadAllText(filePath, Encoding.UTF8);
        }

        public static bool CreateTextFile(string filePath, string content)
        {
            LoggerService.LogInfo($"TxtService.CreateTextFile");
            try
            {
                var rep = Path.GetDirectoryName(filePath);
                if (rep != "")
                {
                    if (!Path.Exists(rep))
                    {
                        Directory.CreateDirectory(rep!);
                    }
                }

                if (File.Exists(filePath))
                    File.Delete(filePath);
                File.WriteAllText(filePath, content, new UTF8Encoding(true));
                return true;
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"TxtService.CreateTextFile : {ex.Message}");
                return false;
            }
        }

        private static bool IsTextFile(string path, int sampleSize = 1024)
        {
            LoggerService.LogInfo($"TxtService.IsTextFile : {path}");

            byte[] buffer = new byte[sampleSize];
            int bytesRead;

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                bytesRead = fs.Read(buffer, 0, buffer.Length);
            }

            // Si le fichier est vide, on le considère comme texte
            if (bytesRead == 0) return true;

            // Vérifier s'il contient beaucoup de caractères non imprimables
            int nonPrintableCount = 0;
            for (int i = 0; i < bytesRead; i++)
            {
                byte b = buffer[i];
                // Autorise : tab(9), LF(10), CR(13), caractères imprimables ASCII (32-126) et UTF-8 multibyte (>127)
                if (!(b == 9 || b == 10 || b == 13 || (b >= 32 && b <= 126) || b >= 128))
                {
                    nonPrintableCount++;
                }
            }

            // Seuil : moins de 5% de caractères non imprimables => fichier texte
            double ratio = (double)nonPrintableCount / bytesRead;
            return ratio < 0.05;
        }
    }
}