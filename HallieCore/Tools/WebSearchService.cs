using ExternalServices;
using HallieDomain;
using System.Text;
using System.Text.Json;

namespace Hallie.Tools
{
    #region Tool
    public class WebSearchTool : ITool
    {
        public string Name => "web_search";
        public string Description => "Trouve sur internet les informations demandées (search, images, videos)";

        private readonly IWebSearchService _service;

        public WebSearchTool(string apiUrl, string apiKey)
        {
            LoggerService.LogInfo("SerperTool");
            var service = new WebSearchService(apiUrl, apiKey);
            _service = service;
        }

        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            LoggerService.LogInfo("SerperTool.ExecuteAsync");


            try
            {
                var query = parameters["query"].ToString();
                var type = parameters["type"].ToString();
                var (bOk, reponse) = await _service.WebSearchAsync(query!, type);

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
                    Description = "L'information à chercher sur internet",
                    Required = true
                },
                new ToolParameter
                {
                    Name = "type",
                    Type = "string",
                    Description = "Le type d'information à chercher sur internet (search, images, videos)",
                    Required = true
                }
            };
        }
    }
    #endregion

    #region Service recherche Web

    #region Interface
    public interface IWebSearchService
    {
        Task<(bool, string)> WebSearchAsync(string query, string? type = "search");
    }
    #endregion

    #region Classe
    public class WebSearchService: IWebSearchService
    {
        private readonly string _apiKey;
        private readonly string _apiUrl;
        public WebSearchService(string apiUrl, string apiKey) 
        {
            _apiUrl = apiUrl;
            _apiKey = apiKey;
        }

        /// <summary>
        /// Code exemple d'appel POST à Serper
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private async Task<string> SearchPostAsync(string query)
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, $"{Params.WebSearchUrl}/search");
            request.Headers.Add("X-API-KEY", _apiKey);
            var content = new StringContent($$"""{"q":"{{query}}","gl":"fr","hl":"fr"}""", null, "application/json");
            request.Content = content;
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var rep = await response.Content.ReadAsStringAsync();
            LoggerService.LogDebug($"SerperService.Search AVANT SummarizeSerperResult: {rep}");
            rep = SummarizeResult(rep);
            LoggerService.LogDebug($"SerperService.Search APRES SummarizeSerperResult: {rep}");
            return rep;
        }

        /// <summary>
        /// Code exemple d'appel GET à Serper
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private async Task<string> SearchGetAsync(string query)
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, $"{Params.WebSearchUrl!}/search?q={query}&gl=fr&hl=fr&apiKey={Params.WebSearchApiKey!}");
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var rep = await response.Content.ReadAsStringAsync();
            LoggerService.LogDebug($"SerperService.Search AVANT SummarizeSerperResult: {rep}");
            rep = SummarizeResult(rep);
            LoggerService.LogDebug($"SerperService.Search APRES SummarizeSerperResult: {rep}");
            return rep;
        }

        /// <summary>
        /// Appel à l'API Serper pour une recherche web (search, images, videos)
        /// </summary>
        /// <param name="query"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public async Task<(bool, string)> WebSearchAsync(string query, string? type = "search")
        {
            try
            {
                LoggerService.LogDebug($"SerperService.WebSearchAsync : {query} / {type}");
                using var http = new HttpClient();

                http.DefaultRequestHeaders.Add("X-API-KEY", _apiKey);

                var payload = new
                {
                    q = query,
                    gl = "fr",
                    hl = "fr"
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await http.PostAsync($"{_apiUrl}/{type}", content);
                response.EnsureSuccessStatusCode();

                var rep = await response.Content.ReadAsStringAsync();

                if (type == "search")
                {
                    rep = SummarizeResult(rep);
                }
                else if (type == "images")
                {
                    List<ImageResult> lstImgs = new();
                    lstImgs = ParseImages(rep);
                    StringBuilder sb = new();
                    sb.AppendLine("Voici les images trouvées:");
                    foreach (var img in lstImgs.Take(5))
                    {
                        sb.AppendLine($"- {img.Title} : {img.ImageUrl} (Source: {img.Source})");

                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = img.ImageUrl,
                            UseShellExecute = true
                        });
                    }
                    rep = sb.ToString();
                }
                else if (type == "videos")
                {
                    List<VideoResult> lstVideos = new();
                    lstVideos = ParseVideos(rep);
                    StringBuilder sb = new();
                    sb.AppendLine("Voici les vidéos trouvées:");
                    foreach (var vid in lstVideos.Take(5))
                    {
                        sb.AppendLine($"- {vid.Title} : {vid.Url} (Source: {vid.Source})");

                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = vid.Url,
                            UseShellExecute = true
                        });
                    }
                    rep = sb.ToString();
                }

                LoggerService.LogDebug($"SerperService.WebSearchAsync APRES SummarizeSerperResult: {rep}");
                return (true, rep);
            }
            catch(Exception ex)
            {
                return (false,ex.Message);
            }
        }

        /// <summary>
        /// Résumé des résultats organiques de Serper
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        private string SummarizeResult(string json)
        {
            try 
            { 
                var doc = JsonDocument.Parse(json);
                var organic = doc.RootElement.GetProperty("organic");

                var sb = new StringBuilder();

                foreach (var result in organic.EnumerateArray().Take(5))
                {
                    sb.AppendLine($"Source: {result.GetProperty("link").GetString()}");
                    sb.AppendLine(result.GetProperty("snippet").GetString());
                    sb.AppendLine();
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                LoggerService.LogDebug($"SerperService.SummarizeSerperResult Exception: {ex.Message}");
                return ex.Message;
            }
        }

        /// <summary>
        /// Parsing des résultats images de Serper
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        private List<ImageResult> ParseImages(string json)
        {
            try
            {
                var doc = JsonDocument.Parse(json);
                var images = doc.RootElement.GetProperty("images");

                var results = new List<ImageResult>();

                foreach (var img in images.EnumerateArray().Take(5))
                {
                    results.Add(new ImageResult
                    {
                        Title = img.GetProperty("title").GetString()!,
                        ImageUrl = img.GetProperty("imageUrl").GetString()!,
                        ThumbnailUrl = img.GetProperty("thumbnailUrl").GetString()!,
                        Source = img.GetProperty("source").GetString()!
                    });
                }

                return results;
            }            
            catch(Exception ex)
            {
                LoggerService.LogDebug($"SerperService.ParseImages Exception: {ex.Message}");
                return new List<ImageResult>();
            }
}

        /// <summary>
        /// Parsing des résultats vidéos de Serper
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        private List<VideoResult> ParseVideos(string json)
        {
            try 
            { 
                var doc = JsonDocument.Parse(json);
                var videos = doc.RootElement.GetProperty("videos");

                var results = new List<VideoResult>();

                foreach (var v in videos.EnumerateArray().Take(5))
                {
                    results.Add(new VideoResult
                    {
                        Title = v.GetProperty("title").GetString()!,
                        Url = v.GetProperty("link").GetString()!,
                        Snippet = v.GetProperty("snippet").GetString()!,
                        Duration = v.GetProperty("duration").GetString()!,
                        Channel = v.GetProperty("channel").GetString()!,
                        Source = v.GetProperty("source").GetString()!
                    });
                }

                return results;
            }
            catch (Exception ex)
            {
                LoggerService.LogDebug($"SerperService.ParseVideos Exception: {ex.Message}");
                return new List<VideoResult>();
            }
        }

        #region Classes privées annexes
        private class ImageResult
        {
            public string Title { get; set; } = "";
            public string ImageUrl { get; set; } = "";
            public string ThumbnailUrl { get; set; } = "";
            public string Source { get; set; } = "";
        }

        private class VideoResult
        {
            public string Title { get; set; } = "";
            public string Url { get; set; } = "";
            public string Snippet { get; set; } = "";
            public string Duration { get; set; } = "";
            public string Channel { get; set; } = "";
            public string Source { get; set; } = "";
        }
        #endregion

    }
    #endregion

    #endregion
}
