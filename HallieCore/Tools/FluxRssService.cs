using ExternalServices;
using Hallie.Services;
using Hallie.Tools;
using System.Net;
using System.ServiceModel.Syndication;
using System.Text;
using System.Xml;
using HallieDomain;

namespace HallieCore.Tools
{
    public class PressReviewTool:ITool
    {
        public string Name => "press_review";
        public string Description => "Outil pour créer une revue de presse (actualités) sur la base de flux RSS.";

        private RssReaderService _Service;
        private string _Location;

        private List<string> _Lst = new();
        private List<string> _Titres = new();
        public PressReviewTool() 
        {
            LoggerService.LogInfo("PressReviewTool");

            _Service = new RssReaderService();
            _Location = Params.WeatherVille!;

            var rssMonde = "https://www.france24.com/fr/rss";
            var rssInnovations = "https://www.france24.com/fr/tag/entr/rss";
            var rssFrance = "https://www.france24.com/fr/france/rss";
            var rssEconomie = "https://www.france24.com/fr/%C3%A9co-tech/rss";
            var rssEnvironnement = "https://www.france24.com/fr/environnement/rss";

            _Lst.Add(rssMonde);
            _Lst.Add(rssFrance);
            _Lst.Add(rssEnvironnement);
            _Lst.Add(rssEconomie);
            _Lst.Add(rssInnovations);

            _Titres.Add($"🌤️ Météo à {_Location}");
            _Titres.Add("🌍 Monde");
            _Titres.Add("🇫🇷 France");
            _Titres.Add("🌎 Environnement");
            _Titres.Add("💹 Economie");
            _Titres.Add("🔬 Innovations");
        }

        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            LoggerService.LogInfo("PressReviewTool.ExecuteAsync");

            try
            {
                var _toolRegistry = new ToolRegistry();
                var _OllamaClient = new OllamaClient(_toolRegistry, Params.OllamaLlmUrl!, Params.OllamaLlmModel!, Params.OllamaLlmModelTemperature, null, null);
                var meteoService = new WeatherApiComService(Params.WeatherUrl!, Params.WeatherApiKey!);
                var meteo = await meteoService.GetWeatherAsync(_Location);


                var rss = await _Service.ReadAsync(_Lst, 0);
                var html = EmailHtmlBuilder.BuildDigestHtml(meteo,_Titres, rss);
                var filename = "revue_presse.html";
                string filePath = System.IO.Path.Combine(Environment.CurrentDirectory, filename);
                var b = TxtService.CreateTextFile(filePath, html);

                if (!b)
                {
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        reponse = "",
                        error = $"Une erreur s'est produite dans la création du fichier {filename}"
                    });
                }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });

                return JsonService.Serialize(new
                {
                    ok = true,
                    reponse = $"Revue de presse réalisée pour les thématiques suivantes : Météo, {string.Join(", ", _Titres)}:\nLe fichier a été généré et il se trouve : {filePath}",
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
            return Array.Empty<ToolParameter>();
        }
    }

    public sealed class RssReaderService
    {
        public async Task<List<List<RssArticle>>> ReadAsync(List<string> lstFeedUrl, int maxItems=0, CancellationToken ct = default)
        {
            List<List<RssArticle>> lst = new();
            foreach (string item in lstFeedUrl)
            {
                var resultat = await ReadAsync(item);
                lst.Add(resultat);
            }
            return lst;
        }
        public async Task<List<RssArticle>> ReadAsync(string feedUrl, int maxItems = 0, CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                using var reader = XmlReader.Create(feedUrl, new XmlReaderSettings
                {
                    Async = false,
                    DtdProcessing = DtdProcessing.Ignore
                });

                var feed = SyndicationFeed.Load(reader);
                if (feed == null)
                    return new List<RssArticle>();

                var items = feed.Items
                    .OrderByDescending(x => x.PublishDate)
                    .Where(x => x.PublishDate.Date >= DateTime.Now.Date.AddDays(-3))
                    // .Take(Math.Max(1, maxItems))
                    .Select(i => new RssArticle
                    {
                        Title = i.Title?.Text?.Trim() ?? "(sans titre)",
                        Link = i.Links.FirstOrDefault()?.Uri?.ToString() ?? "",
                        Summary = StripHtml(i.Summary?.Text ?? (i.Content is TextSyndicationContent c ? c.Text : "")),
                        PublishDate = i.PublishDate != DateTimeOffset.MinValue ? i.PublishDate : null
                    })
                    .ToList();

                return items;
            }, ct);
        }
        private static string StripHtml(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "";

            var withoutTags = System.Text.RegularExpressions.Regex.Replace(input, "<.*?>", " ");
            withoutTags = System.Net.WebUtility.HtmlDecode(withoutTags);
            return System.Text.RegularExpressions.Regex.Replace(withoutTags, @"\s+", " ").Trim();
        }
    }

    public static class EmailHtmlBuilder
    {
        public static string BuildDigestHtml(List<string> title, List<List<RssArticle>> rubriques)
        {
            WeatherData? meteo = null;
            return BuildDigestHtml(meteo, title, rubriques);
        }
        public static string BuildDigestHtml(WeatherData? meteo, List<string> title, List<List<RssArticle>> rubriques)
        {
            var sb = new StringBuilder();

            sb.Append(@$"
                <div style=""font-family:Arial,sans-serif;max-width:600px;margin:0 auto;background:#f9f9f9;padding:20px;color:#333;"">
                  <div style=""text-align:center;margin-bottom:20px;"">
                    <h1 style=""margin:0;font-size:24px;color:#222;"">Newsletter du {DateTime.Now.ToShortDateString()}</h1>
                  </div>");

            if(meteo != null)
            {
                sb.Append(@$"
                  <div style=""margin-bottom:20px;"">
                    <h2 style=""font-size:22px;color:#0099ff;margin:0 0 10px;"">{title[0]}</h2>
                    <p style=""margin:0;"">");
                sb.Append(WebUtility.HtmlEncode($"Température : {meteo.Temperature}°c – Ressenti : {meteo.FeelsLike}°c – Temps : {meteo.Description} – Vent : {meteo.WindSpeed} km/h – Humidité : {meteo.Humidity}%"));

                sb.Append(@"</p></div>");

            }



            int i = 0;
            foreach (var articles in rubriques)
            {
                i++;
                if (articles.Count == 0)
                    continue;

                sb.Append(@$"
                  <div style=""margin-bottom:20px;"">
                    <h2 style=""font-size:22px;color:#0099ff;margin:0 0 10px;"">{title[i]}</h2>
                    <p style=""margin:0;"">");




                foreach (var article in articles)
                {
                    var dateText2 = article.PublishDate?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "";
                    var dateText = "";
                    if (!string.IsNullOrWhiteSpace(dateText2))
                    {
                        dateText = @"<p style=""margin:0 0 10px 0; font-size:11px; color:#666666;"">";
                        dateText += WebUtility.HtmlEncode(dateText2);
                        dateText += @"</p>";
                    }

                    sb.Append($@"
                        <div style=""margin-bottom:10px;"">
                            <strong>
                                {article.Title}
                            </strong>
                            <br><br>
                            {dateText}
                            {article.Summary}
                            <br><br>
                            <a href=""{article.Link}"" style=""color:#0066cc;"">
                                Lire l'article
                            </a>
                        </div>");

                }

                sb.Append($@"</div>");
            }
            sb.Append(@$"
                  <div style=""text-align:center;font-size:12px;color:#777;"">
                    © France 24 – © {Params.AvatarName!} – {DateTime.Now.ToString("dd/MM/yyyy")}
                  </div>
                </div>
            ");

            return sb.ToString();
        }

        public static string BuildPlainText(string title, IEnumerable<RssArticle> articles)
        {
            var sb = new StringBuilder();
            sb.AppendLine(title);
            sb.AppendLine(new string('=', title.Length));
            sb.AppendLine();

            foreach (var a in articles)
            {
                sb.AppendLine(a.Title);
                if (a.PublishDate.HasValue)
                    sb.AppendLine(a.PublishDate.Value.LocalDateTime.ToString("dd/MM/yyyy HH:mm"));
                if (!string.IsNullOrWhiteSpace(a.Summary))
                    sb.AppendLine(a.Summary);
                if (!string.IsNullOrWhiteSpace(a.Link))
                    sb.AppendLine(a.Link);

                sb.AppendLine();
                sb.AppendLine(new string('-', 60));
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }

    public sealed class RssArticle
    {
        public string Title { get; set; } = "";
        public string Link { get; set; } = "";
        public string Summary { get; set; } = "";
        public DateTimeOffset? PublishDate { get; set; }
    }
    public sealed class RssEmailDigestRequest
    {
        public string FeedUrl { get; set; } = "";
        public int MaxItems { get; set; } = 10;
        public string? Topic { get; set; }
    }
    public sealed class RssEmailDigestResult
    {
        public string Subject { get; set; } = "";
        public string HtmlBody { get; set; } = "";
        public string PlainTextBody { get; set; } = "";
        public int ItemCount { get; set; }
    }
}
