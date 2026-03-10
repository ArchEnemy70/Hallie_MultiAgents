using ExternalServices;
using Fizzler.Systems.HtmlAgilityPack;
using HtmlAgilityPack;
using System.Net;
using System.Text.Json;

namespace Hallie.Tools
{


    #region Tool
    public class WebFetchTool : ITool
    {
        public string Name => "web_fetch";
        public string Description => "Outil pour extraire des informations d'une page web.";
        private WebFetchService _Service;
        public WebFetchTool()
        {
            LoggerService.LogInfo("WebFetchTool");
            _Service = new();
        }

        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            LoggerService.LogInfo("WebFetchTool.ExecuteAsync");

            try
            {
                var url = parameters.ContainsKey("url")
                    ? parameters["url"].ToString()
                    : "";
                var typeExtract = parameters.ContainsKey("extract")
                    ? parameters["extract"].ToString()
                    : "";

                var selector = parameters.ContainsKey("selector")
                    ? parameters["selector"].ToString()
                    : "";

                var mode = parameters.ContainsKey("mode")
                    ? parameters["mode"].ToString()
                    : "";

                var resultat = await _Service.FetchAsync(url!, typeExtract!, selector!, mode!);
                if (resultat == null)
                {
                    LoggerService.LogWarning($"WebFetchTool.ExecuteAsync : Échec de l'extraction de la page {url}");
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        reponse = "",
                        error = $"Échec de l'extraction de la page {url}."
                    });
                }

                return JsonService.Serialize(new
                {
                    ok = true,
                    reponse = $"{resultat}",
                    error = ""
                });

            }

            catch (Exception ex)
            {
                LoggerService.LogError($"WebFetchTool.ExecuteAsync : {ex.Message}");
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
                    Name = "url",
                    Type = "string",
                    Description = "URL de la page web où l'on va extraire des données.",
                    Required = true
                },
                new ToolParameter
                {
                    Name = "extract",
                    Type = "string",
                    Description = "Type d'extraction (readableText | links | tables | css).",
                    Required = true
                },
                new ToolParameter
                {
                    Name = "selector",
                    Type = "string",
                    Description = "élément ciblé pour l'extraction. Ne renseigner que si extract = css (exemple : #price-table ou main article ou .article-body",
                    Required = false
                },
                new ToolParameter
                {
                    Name = "mode",
                    Type = "string",
                    Description = "Mode choisi : text|html|links|tables|all (défaut: all). Ne renseigner que si extract = css ",
                    Required = false
                }
            };
        }
    }
    #endregion

    #region Service
    public sealed class WebFetchService
    {
        private readonly HttpClient _http;

        public WebFetchService()
        {
            _http = new();
        }

        public async Task<string?> FetchAsync(string url, string extract,string? selector, string? mode, CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url))
                    throw new ArgumentException("URL is required");

                Uri baseUri = new Uri(url);
                var html = await _http.GetStringAsync(url, ct);

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                object r;
                switch(extract)
                {
                    case "readableText":
                        r = new { readableText = ExtractReadableText(doc) };
                        break;
                    case "links":
                        r = new { links = ExtractLinks(doc, baseUri) };
                        break;
                    case "tables":
                        r = new { tables = ExtractTables(doc.DocumentNode) };
                        break;
                    case "css":
                        r = ExtractCss(doc, baseUri, selector, mode);
                        break;
                    default:
                        throw new ArgumentException("Invalid extract type");
                }

                return JsonSerializer.Serialize(r, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex) 
            {
                LoggerService.LogError($"WebFetchService.FetchAsync : {ex.Message}");
                return null;
            }
        }

        private static object ExtractCss(HtmlDocument doc, Uri baseUri, string? selector, string? mode)
        {
            if (string.IsNullOrWhiteSpace(selector))
                return new { error = "selector is required when extract=css" };

            mode = (mode ?? "all").Trim().ToLowerInvariant();

            // Fizzler: requête CSS sur le DocumentNode
            // QuerySelectorAll retourne IEnumerable<HtmlNode>
            var nodes = doc.DocumentNode.QuerySelectorAll(selector).ToList();

            if (nodes.Count == 0)
            {
                return new
                {
                    url = baseUri.ToString(),
                    selector,
                    found = false,
                    mode,
                    extract = (object?)null
                };
            }

            // On agrège : si plusieurs noeuds matchent, on concatène / fusionne
            // (Pour des tableaux, souvent 1 noeud. Pour .article-body, peut y en avoir plusieurs)
            var combined = new HtmlDocument();
            var container = combined.CreateElement("div");
            combined.DocumentNode.AppendChild(container);

            foreach (var n in nodes)
            {
                // CloneNode true: copie profonde
                container.AppendChild(n.CloneNode(true));
            }

            var root = container;

            var result = new Dictionary<string, object?>
            {
                ["url"] = baseUri.ToString(),
                ["selector"] = selector,
                ["found"] = true,
                ["mode"] = mode
            };

            object extractObj = mode switch
            {
                "text" => new { text = CleanText(root.InnerText) },
                "html" => new { html = root.InnerHtml },
                "links" => new { links = ExtractLinks(root, baseUri) },
                "tables" => new { tables = ExtractTables(root) },
                "all" => new
                {
                    text = CleanText(root.InnerText),
                    html = root.InnerHtml,
                    links = ExtractLinks(root, baseUri),
                    tables = ExtractTables(root)
                },
                _ => new { error = "Invalid mode. Allowed: text|html|links|tables|all" }
            };

            result["extract"] = extractObj;
            return result;
        }
        private static string ExtractReadableText(HtmlDocument doc)
        {
            RemoveNodes(doc, "//script|//style|//noscript");

            var body = doc.DocumentNode.SelectSingleNode("//body");
            if (body == null) return "";

            return CleanText(body.InnerText);
        }
        private static void RemoveNodes(HtmlDocument doc, string xpath)
        {
            var nodes = doc.DocumentNode.SelectNodes(xpath);
            if (nodes == null) return;
            foreach (var n in nodes) n.Remove();
        }
        private static List<string> ExtractLinks(HtmlDocument doc, Uri baseUri) => ExtractLinks(doc.DocumentNode, baseUri);
        private static List<string> ExtractLinks(HtmlNode root, Uri baseUri)
        {
            var nodes = root.SelectNodes(".//a[@href]");
            if (nodes == null) return new List<string>();

            return nodes
                .Select(a => a.GetAttributeValue("href", "").Trim())
                .Where(h => !string.IsNullOrWhiteSpace(h))
                .Select(h => NormalizeUrl(baseUri, h))
                .Where(u => u != null)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()!;
        }
        private static List<object> ExtractTables(HtmlNode root)
        {
            var tables = root.SelectNodes(".//table");
            var result = new List<object>();
            if (tables == null) return result;

            foreach (var table in tables)
            {
                var rows = table.SelectNodes(".//tr");
                if (rows == null || rows.Count == 0) continue;

                // Header: première ligne si th, sinon auto Column1..n
                var firstCells = rows[0].SelectNodes(".//th|.//td");
                if (firstCells == null || firstCells.Count == 0) continue;

                bool headerIsTh = rows[0].SelectNodes(".//th")?.Count > 0;

                var headers = firstCells.Select(c => CleanText(c.InnerText)).ToList();
                if (!headerIsTh)
                    headers = headers.Select((_, i) => $"Column{i + 1}").ToList();

                var data = new List<Dictionary<string, string>>();

                int startRow = headerIsTh ? 1 : 0;
                for (int i = startRow; i < rows.Count; i++)
                {
                    var cells = rows[i].SelectNodes(".//td|.//th");
                    if (cells == null || cells.Count == 0) continue;

                    var row = new Dictionary<string, string>();
                    for (int j = 0; j < headers.Count && j < cells.Count; j++)
                        row[headers[j]] = CleanText(cells[j].InnerText);

                    // ignore lignes vides
                    if (row.Values.All(string.IsNullOrWhiteSpace)) continue;

                    data.Add(row);
                }

                result.Add(new
                {
                    headers,
                    rows = data
                });
            }

            return result;
        }

        #region  Outils
        private static string CleanText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            return HtmlEntity.DeEntitize(text)
                .Replace("\r", "")
                .Replace("\n", " ")
                .Replace("\t", " ")
                .Trim();
        }
        private static string? NormalizeUrl(Uri baseUri, string href)
        {
            // ignore anchors/mailto/javascript
            if (href.StartsWith("#")) return null;
            if (href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) return null;
            if (href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)) return null;

            if (Uri.TryCreate(href, UriKind.Absolute, out var abs))
                return abs.ToString();

            if (Uri.TryCreate(baseUri, href, out var rel))
                return rel.ToString();

            return null;
        }
        private static string Fail(string msg)
            => JsonSerializer.Serialize(new { success = false, error = msg }, new JsonSerializerOptions { WriteIndented = true });
        private static bool TryValidateUrl(string url, out Uri? uri, out string error)
        {
            error = "";
            uri = null!;

            if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
            {
                error = "Invalid URL";
                return false;
            }

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                error = "Only http/https allowed";
                return false;
            }

            return true;
        }
        private static bool IsPrivateOrLocal(Uri uri)
        {
            if (uri.IsLoopback) return true;

            // Si hostname est une IP, on teste si privée
            if (IPAddress.TryParse(uri.Host, out var ip))
                return IsPrivateIp(ip);

            // Si c'est un nom DNS, version "simple" : on ne résout pas ici.
            // Version pro: DNS resolve + vérifier toutes les IP.
            return false;
        }
        private static bool IsPrivateIp(IPAddress ip)
        {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                var b = ip.GetAddressBytes();
                // 10.0.0.0/8
                if (b[0] == 10) return true;
                // 172.16.0.0/12
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
                // 192.168.0.0/16
                if (b[0] == 192 && b[1] == 168) return true;
                // 127.0.0.0/8
                if (b[0] == 127) return true;
                // 169.254.0.0/16 (link local)
                if (b[0] == 169 && b[1] == 254) return true;
            }

            // IPv6: loopback + link-local
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                if (ip.IsIPv6LinkLocal) return true;
                if (IPAddress.IPv6Loopback.Equals(ip)) return true;
            }

            return false;
        }
        private static string NormalizeUrl(string baseUrl, string href)
        {
            if (Uri.TryCreate(href, UriKind.Absolute, out var abs))
                return abs.ToString();

            var baseUri = new Uri(baseUrl);
            return new Uri(baseUri, href).ToString();
        }
        #endregion
    }
    #endregion
}
