using System.Diagnostics;
using System.Text;

namespace ExternalServices
{
    public static class FilesService
    {
        public static (bool, string) ExtractZip(string zipFileName, bool summaryFiles=false, bool deleteDirectory=true, string path="")
        {
            try
            {
                var summaries = new StringBuilder();

                var extractDirectory = "";
                if (path == "")
                    extractDirectory = Path.Combine(Path.GetTempPath(), "zip_" + Guid.NewGuid());
                else
                    extractDirectory = path;

                if(!Directory.Exists(extractDirectory))
                    Directory.CreateDirectory(extractDirectory);

                System.IO.Compression.ZipFile.ExtractToDirectory(zipFileName, extractDirectory);
                if (summaryFiles)
                {
                    foreach (var innerFile in Directory.GetFiles(extractDirectory, "*.*", SearchOption.AllDirectories))
                    {
                        var filenameInZip = Path.GetFileName(innerFile);
                        LoggerService.LogInfo($"Traitement de la pièce jointe dans {zipFileName} : {filenameInZip}");

                        try
                        {
                            var ext = Path.GetExtension(innerFile).ToLowerInvariant();

                            if (IsExtensionSupport(ext))
                            {
                                summaries.AppendLine(ExtractText(innerFile));
                            }
                            else
                            {
                                summaries.AppendLine($"📎 {filenameInZip} :\n Format de fichier non pris en charge... ");
                            }
                        }
                        catch
                        {
                            summaries.AppendLine($"📎 {filenameInZip} :\n Fichier illisible... ");
                        }
                    }
                }
                if (deleteDirectory)
                {
                    Directory.Delete(extractDirectory, true);
                }
                else
                {
                    summaries.AppendLine($"Dossier d'extraction : {extractDirectory}");
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = extractDirectory,
                        UseShellExecute = true
                    });
                }
                return (true, summaries.ToString());
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"FilesService.ExtractZip : {ex.Message}");
                return (false, ex.Message);
            }
        }
        public static bool IsExtensionSupport(string extension)
        {
            return extension is ".pdf" or ".docx" or ".xlsx" or ".txt" or ".csv" or ".rtf";
        }

        public static string ExtractTextFromBytes(byte[] bytes, string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();

            return ext switch
            {
                ".pdf" => PdfService.ExtractPdfFromBytesNew(bytes),
                ".docx" => DocxService.ExtractDocxFromBytes(bytes),
                ".xlsx" => XlsxService.ExtractXlsxFromBytes(bytes),
                ".txt" => Encoding.UTF8.GetString(bytes),
                _ => throw new NotSupportedException($"Type non supporté : {ext}")
            };
        }

        public static string ExtractText(string fileFullname)
        {
            LoggerService.LogInfo($"FilesService.ExtractText : {fileFullname}");

            if (string.IsNullOrEmpty(fileFullname))
                return string.Empty;

            string ext = System.IO.Path.GetExtension(fileFullname).ToLower();
            return ext switch
            {
                ".pdf" => PdfService.ExtractTextFromPdf(fileFullname),
                ".docx" => DocxService.ExtractTextFromDocx(fileFullname),
                ".xlsx" => XlsxService.ExtractTextFromXlsx(fileFullname),
                ".txt" => File.ReadAllText(fileFullname),
                ".csv" => XlsxService.ExtractTextFromCsv(fileFullname),
                ".rtf" => DocxService.ExtractTextFromRtf(fileFullname),
                _ => TxtService.ExtractTextFromTxt(fileFullname)
            };
        }

        public static (List<string>, List<string>) GetListesFichiers(List<string> fichiers)
        {
            LoggerService.LogInfo($"FilesService.GetListesFichiers");

            // Extensions d’images acceptées
            string[] extensionsImages = ImagesExtensions();

            // Séparer les images
            var images = fichiers
                .Where(f => extensionsImages.Contains(Path.GetExtension(f).ToLower()))
                .ToList();

            var autres = fichiers
                .Where(f => !extensionsImages.Contains(Path.GetExtension(f).ToLower()))
                .ToList();

            if (images.Count > 0)
            {
                LoggerService.LogDebug($"{images.Count} image(s) : ");
                foreach (var d in images)
                {
                    LoggerService.LogDebug($"{d}");
                }
            }

            if (autres.Count > 0)
            {
                LoggerService.LogDebug($"{autres.Count} document(s) : ");
                foreach (var d in autres)
                {
                    LoggerService.LogDebug($"  - {d}");
                }
            }
            return (images, autres);
        }

        private static string[] ImagesExtensions()
        {
            string[] extensionsImages = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".webp" };
            return extensionsImages;
        }
    }
}
