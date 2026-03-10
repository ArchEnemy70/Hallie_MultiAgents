using ExternalServices;
using HallieDomain;
using System.Diagnostics;
using Whisper.net;

namespace Hallie.Tools
{
    #region Tool
    public class TranscribTool : ITool
    {
        public string Name => "audio_video_transcrib";
        public string Description => "Fait la transcription d'un fichier audio ou vidéo avec WHISPER";

        private readonly TranscribService _service;

        public TranscribTool()
        {
            LoggerService.LogInfo("TranscribTool");
            var service = new TranscribService();
            _service = service;
        }

        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            LoggerService.LogInfo("TranscribTool.ExecuteAsync");

            try
            {
                var query = parameters["query"].ToString();
                var fullfilename = parameters["fullfilename"].ToString();
                var path_to_analyse = parameters["path_to_analyse"].ToString();
                var path_analysed = parameters["path_analysed"].ToString();

                bool bOk = false;
                string? reponse;

                if (fullfilename == null || fullfilename == "")
                {
                    // pas un fichier précis indiqué
                    if (path_to_analyse == null || path_to_analyse == "")
                    {
                        // pas de dossier d'origine précis indiqué
                        path_to_analyse = Params.MultimediaPathToAnalyse!;
                    }
                    if (path_analysed == null || path_analysed == "")
                    {
                        // pas de dossier dcible précis indiqué
                        path_analysed = Params.MultimediaPathAnalysed!;
                    }
                    (bOk, reponse) = await _service.Transcrib(path_to_analyse, path_analysed);
                }
                else
                {
                    // Fichier précis indiqué
                    (bOk, reponse) = await _service.Transcrib(fullfilename);
                }
                

                if (!bOk)
                {
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        reponse = "",
                        error = reponse
                    });
                }

                return JsonService.Serialize(new
                {
                    ok = true,
                    reponse = reponse,
                    error = ""
                });

            }
            catch (Exception ex)
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
                    Name = "query",
                    Type = "string",
                    Description = "Le prompt qui indique de faire le résumé de la transcription",
                    Required = true
                },
                new ToolParameter
                {
                    Name = "fullfilename",
                    Type = "string",
                    Description = "Nom complet (repertoire + nom de fichier + extension) de l'image ou video à analyser (si pas explicitement donné, laisser vide)",
                    Required = true
                },
                new ToolParameter
                {
                    Name = "path_to_analyse",
                    Type = "string",
                    Description = "Chemin du répertoire qui contient les images ou videos à analyser (si pas explicitement donné, laisser vide)",
                    Required = true
                },
                new ToolParameter
                {
                    Name = "path_analysed",
                    Type = "string",
                    Description = "Chemin du répertoire où déplacer les images ou les videos une fois qu'elles ont été analysées (si pas explicitement donné, laisser vide)",
                    Required = true
                }
            };
        }
    }
    #endregion

    #region Service Transcription d'une video ou d'un audio
    public class TranscribService
    {
        public TranscribService()
        {

        }

        /// <summary>
        /// Transcrib
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public async Task<(bool, string)> Transcrib(string imagesPathToAnalyse, string imagesPathAnalysed)
        {
            LoggerService.LogDebug($"TranscribService.Transcrib");
            return await Traitement(imagesPathToAnalyse, imagesPathAnalysed);
        }
        public async Task<(bool, string)> Transcrib(string fullfilename)
        {
            LoggerService.LogDebug($"TranscribService.Transcrib");
            return await Traitement("","",fullfilename);
        }

        private async Task<(bool, string)> Traitement(string imagesPathToAnalyse, string imagesPathAnalysed, string fullfilename="")
        {
            try
            {

                if (!System.IO.Directory.Exists(imagesPathToAnalyse))
                {
                    LoggerService.LogWarning($"Le dossier {imagesPathToAnalyse} n'existe pas");
                    return (false, $"Le dossier {imagesPathToAnalyse} n'existe pas");
                }

                #region Conversion de la video / audio en wav
                var fileFullPath = "";
                if (fullfilename == "")
                {
                    var lst = GetFilesFromDirectory(imagesPathToAnalyse);
                    if (lst.Count == 0)
                    {
                        LoggerService.LogWarning($"Le dossier {imagesPathToAnalyse} ne contient pas de fichier audio ou video");
                        return (false, $"Le dossier {imagesPathToAnalyse} ne contient pas de fichier audio ou video");
                    }
                    fileFullPath = lst.FirstOrDefault();
                }
                else
                {
                    fileFullPath = fullfilename;
                }

                var audioPath = Path.ChangeExtension(fileFullPath, ".wav");
                var b = ConversionFile(fileFullPath!, audioPath!);
                #endregion

                #region On demande la transcription
                var rep = await TranscribeWithWhisperNetAsync(audioPath!);
                #endregion

                #region Deplacer le fichier original
                if (imagesPathAnalysed != "" && imagesPathAnalysed != imagesPathToAnalyse)
                {
                    List<string> lstDone = new();
                    lstDone.Add(fileFullPath!);
                    DeplaceFile(lstDone, imagesPathAnalysed);
                    System.IO.File.Delete(fileFullPath!);
                }
                System.IO.File.Delete(audioPath!);
                #endregion

                return (true, rep);
            }
            catch (Exception ex) 
            {
                return (false, ex.Message);
            }
        }

        private bool ConversionFile(string fileFullPath, string audioPath)
        {
            LoggerService.LogDebug("TranscribService.ConversionFile");
            try
            {
                string ffmpegArgs = "";

                ffmpegArgs = $"-y -i \"{fileFullPath}\" -vn -ac 1 -ar 16000 -f wav \"{audioPath}\"";

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "ffmpeg",
                        Arguments = ffmpegArgs,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();

                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    LoggerService.LogError($"FFmpeg error: {stderr}");
                    return false;
                }

                return true;

            }
            catch (Exception ex)
            {
                LoggerService.LogError($"{ex.Message}");
                return false;

            }
        }
        private static async Task<string> TranscribeWithWhisperNetAsync(string wavPath, string modelPath = @"Models\ggml-base.bin", string language = "auto")
        {
            LoggerService.LogDebug("TranscribService.TranscribeWithWhisperNetAsync");

            if (!File.Exists(wavPath))
            {
                LoggerService.LogWarning($"Le fichier n'existe pas : {wavPath}");
                return $"Le fichier n'existe pas : {wavPath}";
            }

            if (!File.Exists(modelPath))
            {
                LoggerService.LogWarning($"Le Model n'existe pas : {modelPath}");
                return $"Le Model n'existe pas : {modelPath}";
            }


            using var whisperFactory = WhisperFactory.FromPath(modelPath);

            using var processor = whisperFactory
                .CreateBuilder()
                .WithLanguage(language)   // "fr" ou "auto"
                .Build();

            using var fileStream = File.OpenRead(wavPath);

            var sb = new System.Text.StringBuilder();

            await foreach (var result in processor.ProcessAsync(fileStream))
            {
                sb.Append(result.Text);
                sb.Append(' ');
            }

            LoggerService.LogDebug(sb.ToString());
            return sb.ToString().Trim();
        }
        private static List<string> GetFilesFromDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
                throw new DirectoryNotFoundException(directoryPath);

            string[] extensions = { ".mpeg", ".mp4", ".avi", ".mkv", ".mp3" };

            var lst = Directory.EnumerateFiles(directoryPath, "*.*", SearchOption.TopDirectoryOnly)
                            .Where(file => extensions.Contains(
                                Path.GetExtension(file),
                                StringComparer.OrdinalIgnoreCase));

            return lst.ToList();
        }
        private static void DeplaceFile(List<string> lstImages, string destinationDirectory, bool overwrite = true)
        {
            if (System.IO.Directory.Exists(destinationDirectory) == false)
                Directory.CreateDirectory(destinationDirectory);

            foreach (var imagePath in lstImages)
            {
                var fileName = Path.GetFileName(imagePath);
                var destPath = Path.Combine(destinationDirectory, fileName);

                File.Copy(imagePath, destPath, overwrite);
                if (System.IO.File.Exists(destPath))
                {
                    System.IO.File.Delete(imagePath);
                    LoggerService.LogDebug($"Image déplacée : {destPath}");
                }
            }
        }

    }
    #endregion

}
