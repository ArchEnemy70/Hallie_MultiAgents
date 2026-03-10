using ExternalServices;
using HallieDomain;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Hallie.Tools
{
    #region Tool
    public class PicturesAnalyseTool : ITool
    {
        public string Name => "pictures_video_analyse";
        public string Description => "Analyse d'images ou de vidéo par un modèle qui peut lire des images";

        private readonly PicturesAnalyseService _service;

        public PicturesAnalyseTool()
        {
            LoggerService.LogInfo("PicturesAnalyseTool");
            var service = new PicturesAnalyseService();
            _service = service;
        }

        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            LoggerService.LogInfo("PicturesAnalyseTool.ExecuteAsync");

            try
            {
                var query = parameters["query"].ToString();
                var type = parameters["type"].ToString();

                var fullfilename = parameters["fullfilename"].ToString();
                var path_to_analyse = parameters["path_to_analyse"].ToString();
                var path_analysed = parameters["path_analysed"].ToString();

                var decoupage = parameters["decoupage"].ToString();
                if(decoupage == null || decoupage == "")
                {
                    decoupage = "scene";
                }

                bool bOk=false;
                string? reponse;

                if (fullfilename == null || fullfilename == "")
                {
                    // pas un fichier précis indiqué
                    if (path_to_analyse == null || path_to_analyse == "")
                    {
                        // pas de dossier d'origine précis indiqué
                        path_to_analyse = Params.MultimediaPathToAnalyse!;
                    }
                    if(path_analysed == null || path_analysed == "")
                    {
                        // pas de dossier dcible précis indiqué
                        path_analysed = Params.MultimediaPathAnalysed!;
                    }
                    (bOk, reponse) = await _service.Analyse(query!, type!, path_to_analyse, path_analysed, Params.OllamaLlmUrl!, Params.OllamaLlmModelVision!, decoupage);
                }
                else
                {
                    // Fichier précis indiqué
                    (bOk, reponse) = await _service.Analyse(query!, type!, fullfilename, Params.OllamaLlmUrl!, Params.OllamaLlmModelVision!, decoupage);
                }


                // On retourne le contexte textuel
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
                    Description = "Le prompt à générer au modèle d'analyse d'images",
                    Required = true
                },
                new ToolParameter
                {
                    Name = "type",
                    Type = "string",
                    Description = "Le type d'analyse : image ou video",
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
                },
                new ToolParameter
                {
                    Name = "decoupage",
                    Type = "string",
                    Description = "Le découpage de la video : seconds (si demandé par l'utilisateur) ou scene (par défaut)",
                    Required = true
                }
            };
        }
    }
    #endregion

    #region Service Analyse des images
    public class PicturesAnalyseService
    {
        public PicturesAnalyseService()
        {

        }

        /// <summary>
        /// Code exemple d'appel POST à Serper
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public async Task<(bool, string)> Analyse(string query, string type, string imagesPathToAnalyse, string imagesPathAnalysed, string ollamaUrl, string modeleVision, string decoupage="scene")
        {
            if (type == "image")
            {
                return await AnalyseImages(query, imagesPathToAnalyse, imagesPathAnalysed, ollamaUrl, modeleVision);
            }
            else if(type == "video")
            {
                return await AnalyseVideo(query, imagesPathToAnalyse, imagesPathAnalysed, "",ollamaUrl, modeleVision, decoupage);
            }

            return (false,$"Type d'analyse inconnu : {type}");
        }
        public async Task<(bool, string)> Analyse(string query, string type, string fullfilename, string ollamaUrl, string modeleVision, string decoupage = "scene")
        {
            if (type == "image")
            {
                return await AnalyseImages(query, fullfilename, ollamaUrl, modeleVision);
            }
            else if (type == "video")
            {
                return await AnalyseVideo(query, "","", fullfilename, ollamaUrl, modeleVision, decoupage);
            }

            return (false, $"Type d'analyse inconnu : {type}");
        }
        
        #region Les images
        private async Task<(bool, string)> AnalyseImages(string query, string fullfilename, string ollamaUrl, string modeleVision, bool isSauvegardePictures = true)
        {
            var lst64 = new List<string>();

            if (!System.IO.File.Exists(fullfilename))
            {
                LoggerService.LogWarning($"Le fichier {fullfilename} n'existe pas");
                return (false, $"Le fichier {fullfilename} n'existe pas");
            }

            var imageBase64 = ImageToBase64(fullfilename);
            lst64.Add(imageBase64);

            var payload = new
            {
                model = modeleVision,
                prompt = query,
                images = lst64.ToArray(),
                stream = false
            };

            var json = JsonSerializer.Serialize(payload);

            using var client = new HttpClient();
            client.Timeout = System.TimeSpan.FromSeconds(300);
            client.BaseAddress = new Uri($"{ollamaUrl}");

            var response = await client.PostAsync(
                "/api/generate",
                new StringContent(json, Encoding.UTF8, "application/json")
            );

            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();

            return (true, responseJson);
        }
        private async Task<(bool, string)> AnalyseImages(string query, string imagesPathToAnalyse, string imagesPathAnalysed, string ollamaUrl, string modeleVision, bool isSauvegardePictures = true)
        {
            var lst64 = new List<string>();
            var lst = GetImagesFromDirectory(imagesPathToAnalyse);
            if (lst.Count == 0)
            {
                LoggerService.LogWarning($"Le dossier {imagesPathToAnalyse} ne contient pas de fichier image");
                return (false, $"Le dossier {imagesPathToAnalyse} ne contient pas de fichier image");
            }
            foreach (var imgPath in lst)
            {
                LoggerService.LogDebug($"Image trouvée : {imgPath}");
                var imageBase64 = ImageToBase64(imgPath);
                lst64.Add(imageBase64);
            }

            var payload = new
            {
                model = modeleVision,
                prompt = query,
                images = lst64.ToArray(),
                stream = false
            };

            var json = JsonSerializer.Serialize(payload);

            using var client = new HttpClient();
            client.Timeout = System.TimeSpan.FromSeconds(300);
            client.BaseAddress = new Uri($"{ollamaUrl}");

            var response = await client.PostAsync(
                "/api/generate",
                new StringContent(json, Encoding.UTF8, "application/json")
            );

            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            if (isSauvegardePictures && imagesPathAnalysed != "" && (imagesPathToAnalyse != imagesPathAnalysed))
                DeplaceImages(lst, imagesPathAnalysed);

            return (true, responseJson);
        }
        #endregion

        #region Video (1 seule)
        private async Task<(bool,string)> AnalyseVideo(string query, string imagesPathToAnalyse, string imagesPathAnalysed, string fullfilename, string ollamaUrl, string modeleVision, string typeDecoup, int frameEverySeconds = 10)
        {
            
            if (!System.IO.Directory.Exists(imagesPathToAnalyse))
                return  (false,$"Le dossier {imagesPathToAnalyse} n'existe pas");

            var outputDirectory = System.IO.Path.Combine(imagesPathToAnalyse, "_frames");
            if(!System.IO.Directory.Exists(outputDirectory)  )
                Directory.CreateDirectory(outputDirectory);
            
            var videoFullPath = "";
            if (fullfilename == "")
            {
                var lst = GetVideossFromDirectory(imagesPathToAnalyse);
                if (lst.Count == 0)
                {
                    LoggerService.LogWarning($"Le dossier {imagesPathToAnalyse} ne contient pas de fichier video");
                    return (false, $"Le dossier {imagesPathToAnalyse} ne contient pas de fichier video");
                }
                videoFullPath = lst.FirstOrDefault();
            }
            else
            {
                videoFullPath = fullfilename;
            }

            string ffmpegArgs = "";
            if (typeDecoup == "seconds")
            {
                // frame toutes les x secondes → fps=1/x
                ffmpegArgs =
                    $"-y -i \"{videoFullPath}\" -vf fps=1/{frameEverySeconds} " +
                    $"\"{Path.Combine(outputDirectory, "frame_%05d.jpg")}\"";
            }
            else if(typeDecoup == "scene")
            {
                ffmpegArgs =
                   $"-y -i \"{videoFullPath}\" -vf \"select=gt(scene\\,0.4)\" -vsync vfr " +
                   $"\"{Path.Combine(outputDirectory, "frame_%05d.jpg")}\"";
            }
            else
            {
                return (false,$"Type de découpage inconnu : {typeDecoup}");
            }
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "ffmpeg",
                        Arguments = ffmpegArgs,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

            process.Start();

            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
                return (false,$"FFmpeg error: {stderr}");

            var (bOk,rep) = await AnalyseImages(query, outputDirectory, imagesPathAnalysed, ollamaUrl, modeleVision, false);
            if (!bOk)
            {
                return (false, rep);
            }

            var fileName = Path.GetFileName(videoFullPath);
            var destPath = Path.Combine(imagesPathAnalysed, fileName!);

            File.Copy(videoFullPath!, destPath, true);
            if (System.IO.File.Exists(destPath))
            {
                System.IO.File.Delete(videoFullPath!);
                LoggerService.LogDebug($"Image déplacée : {destPath}");
            }
            System.IO.Directory.Delete(outputDirectory, true);
            return (true, rep);

        }
        #endregion


        private static List<string> GetVideossFromDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
                throw new DirectoryNotFoundException(directoryPath);

            string[] extensions = { ".mpeg", ".mp4", ".avi", ".mkv" };

            var lst = Directory.EnumerateFiles(directoryPath, "*.*", SearchOption.TopDirectoryOnly)
                            .Where(file => extensions.Contains(
                                Path.GetExtension(file),
                                StringComparer.OrdinalIgnoreCase));

            return lst.ToList();
        }

        private static List<string> GetImagesFromDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
                throw new DirectoryNotFoundException(directoryPath);

            string[] extensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff" };

            var lst = Directory.EnumerateFiles(directoryPath, "*.*", SearchOption.TopDirectoryOnly)
                            .Where(file => extensions.Contains(
                                Path.GetExtension(file),
                                StringComparer.OrdinalIgnoreCase));

            return lst.ToList();
        }

        private string ImageToBase64(string imagePath)
        {
            if (!System.IO.File.Exists(imagePath))
            {
                return string.Empty;
            }
            byte[] imageBytes = System.IO.File.ReadAllBytes(imagePath);
            return Convert.ToBase64String(imageBytes);
        }

        private static void DeplaceImages(List<string> lstImages, string destinationDirectory, bool overwrite = true)
        {
            if(System.IO.Directory.Exists(destinationDirectory) == false)
                Directory.CreateDirectory(destinationDirectory);

            foreach (var imagePath in lstImages)
            {
                var fileName = Path.GetFileName(imagePath);
                var destPath = Path.Combine(destinationDirectory, fileName);

                File.Copy(imagePath, destPath, overwrite);
                if(System.IO.File.Exists(destPath))
                {
                    System.IO.File.Delete(imagePath);
                    LoggerService.LogDebug($"Image déplacée : {destPath}");
                }
            }
        }

    }
    #endregion


}
