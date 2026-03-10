using ExternalServices;
using Ical.Net.DataTypes;
using Sprache;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;

namespace Hallie.Tools
{
    #region Tool CalendarCreate
    public class CalendarCreateTool : ITool
    {
        public string Name => "calendar_create";
        public string Description => "Outil pour créer des tâches et des événements dans le calendrier.";

        private ICalendarService _Service;
        private string _TimeZone;
        public CalendarCreateTool(string url, string login, string password, string timeZone)
        {
            LoggerService.LogInfo("CalendarCreateTool");
            _Service = new CalendarCalDavService(url, login, password);
            _TimeZone = timeZone;
        }

        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            LoggerService.LogInfo("CalendarCreateTool.ExecuteAsync");

            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(_TimeZone);

                var title = parameters.ContainsKey("title")
                    ? parameters["title"].ToString()
                    : "";
                var start = parameters.ContainsKey("start")
                    ? parameters["start"].ToString()
                    : "";
                var end = parameters.ContainsKey("end")
                    ? parameters["end"].ToString()
                    : "";
                var location = parameters.ContainsKey("location")
                    ? parameters["location"].ToString()
                    : "";
                var description = parameters.ContainsKey("description")
                    ? parameters["description"].ToString()
                    : "";
                var rrule = parameters.ContainsKey("rrule")
                    ? parameters["rrule"].ToString()
                    : "";
                var attendeesEmails = parameters.ContainsKey("attendeesEmails")
                    ? parameters["attendeesEmails"].ToString()
                    : "";

                DateTime localDateTimeStart = DateTime.ParseExact(start!, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None);
                var offsetStart = tz.GetUtcOffset(localDateTimeStart);
                DateTimeOffset dtoStart = new DateTimeOffset(localDateTimeStart, offsetStart);

                DateTime localDateTimeEnd = DateTime.ParseExact(end!, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None);
                var offsetEnd = tz.GetUtcOffset(localDateTimeEnd);
                DateTimeOffset dtoEnd = new DateTimeOffset(localDateTimeEnd, offsetEnd);

                var newEvent = new CreateEventRequest
                {
                    Title = title!,
                    Start = dtoStart,
                    End = dtoEnd,
                    Location = location,
                    Description = description,
                    Rrule = rrule, 
                    AttendeesEmails = attendeesEmails?
                        .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(e => e.Trim())
                        .Where(e => !string.IsNullOrWhiteSpace(e))
                        .ToList()
                };
                var (bOk, eventCreated) = await _Service.CreateEventAsync(newEvent);
                if (!bOk)
                {
                    LoggerService.LogWarning("CalendarCreateTool.ExecuteAsync : Échec de la création d'un événement dans le calendrier");
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        reponse = "",
                        error = "Échec de la création d'un événement dans le calendrier."
                    });
                }

                return JsonService.Serialize(new
                {
                    ok = true,
                    reponse = "Succès de la création d'un événement dans le calendrier.",
                    error = ""
                });

            }

            catch (Exception ex)
            {
                LoggerService.LogError($"CalendarCreateTool.ExecuteAsync : {ex.Message}");
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
                    Name = "title",
                    Type = "string",
                    Description = "Le titre de l'évévement que l'on va créer.",
                    Required = true
                },
                new ToolParameter
                {
                    Name = "start",
                    Type = "string",
                    Description = "Date et heure de début de l'événement que l'on va créer (format : yyyy-MM-dd HH:mm).",
                    Required = true
                },
                new ToolParameter
                {
                    Name = "end",
                    Type = "string",
                    Description = "Date et heure de fin de l'événement que l'on va créer (format : yyyy-MM-dd HH:mm).",
                    Required = true
                },
                new ToolParameter
                {
                    Name = "location",
                    Type = "string",
                    Description = "Indique où l'événement que l'on va créer aura lieu.",
                    Required = false
                },
                new ToolParameter
                {
                    Name = "description",
                    Type = "string",
                    Description = "Donne des détails sur l'événement que l'on va créer.",
                    Required = false
                },
                new ToolParameter
                {
                    Name = "rrule",
                    Type = "string",
                    Description = "Règles strictes pour les événements récurrents.",
                    Required = false
                },
                new ToolParameter
                {
                    Name = "attendeesEmails",
                    Type = "string",
                    Description = "Liste des adresses mail des invités (dans le cadre d'une réunion qui implique plusieurs personnes). Les éléments de la liste sont séparés par des virgules.",
                    Required = false
                }
            };
        }
    }
    #endregion

    #region Tool CalendarDeleteTool
    public class CalendarDeleteTool : ITool
    {
        public string Name => "calendar_delete";
        public string Description => "Outil pour chercher et supprimer des événements dans le calendrier.";

        private ICalendarService _Service;
        private string _TimeZone;

        public CalendarDeleteTool(string url, string login, string password, string timeZone)
        {
            LoggerService.LogInfo("CalendarDeleteTool");
            _Service = new CalendarCalDavService(url, login, password);
            _TimeZone = timeZone;
        }

        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            LoggerService.LogInfo("CalendarDeleteTool.ExecuteAsync");

            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(_TimeZone);

                DateTimeOffset timeMin;
                DateTimeOffset timeMax;

                var query = parameters.ContainsKey("query")
                    ? parameters["query"].ToString()
                    : "";

                if (parameters.TryGetValue("timemin", out var rawMin) && rawMin is string s1
                    && DateTime.TryParseExact(s1, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtMin))
                {
                    var offset = tz.GetUtcOffset(dtMin);
                    timeMin = new DateTimeOffset(dtMin, offset);
                }
                else
                {
                    timeMin = DateTimeOffset.UtcNow.AddDays(-7); // défaut : 7 jours dans le passé
                }

                if (parameters.TryGetValue("timemax", out var rawMax) && rawMax is string s2 
                    && DateTime.TryParseExact(s2, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtMax))
                {
                    var offset = tz.GetUtcOffset(dtMax);
                    timeMax = new DateTimeOffset(dtMax, offset);
                }
                else
                {
                    timeMax = DateTimeOffset.UtcNow.AddDays(+7); // défaut : 7 jours dans le futur
                }

                var (bOk, lst) = await _Service.SearchAsync(timeMin, timeMax, query);
                if (!bOk)
                {
                    LoggerService.LogError("CalendarDeleteTool.ExecuteAsync : Échec de la recherche pour la suppression dans le calendrier");
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        reponse = "",
                        error = "Échec de la recherche pour la suppression dans le calendrier."
                    });
                }
                if (lst == null || lst.Count == 0)
                {
                    LoggerService.LogWarning("CalendarDeleteTool.ExecuteAsync : Suppression impossible --> aucun élément n'a été trouvé avec les critères indiqués");
                    return JsonService.Serialize(new
                    {
                        ok = true,
                        reponse = "Suppression impossible : aucun élément n'a été trouvé avec les critères indiqués.",
                        error = ""
                    });
                }

                LoggerService.LogDebug($"CalendarDeleteTool.ExecuteAsync: Evenements trouvés --> {lst.Count}");

                var lstUid = lst.Select(e => e.Uid).Where(uid => !string.IsNullOrWhiteSpace(uid)).ToList();
                if (lstUid == null || lstUid.Count == 0)
                {
                    LoggerService.LogWarning("CalendarDeleteTool.ExecuteAsync : Suppression impossible --> aucun élément n'a été trouvé avec les critères indiqués");
                    return JsonService.Serialize(new
                    {
                        ok = true,
                        reponse = "Suppression impossible : aucun élément n'a été trouvé avec les critères indiqués.",
                        error = ""
                    });
                }
                var bSuppr = await _Service.DeleteEventsAsync(lstUid!);
                if (bSuppr)
                {
                    return JsonService.Serialize(new
                    {
                        ok = true,
                        reponse = "Evenements correctement supprimés",
                        error = ""
                    });
                }
                else
                {
                    LoggerService.LogError("CalendarDeleteTool.ExecuteAsync : Échec de la suppression des événements dans le calendrier");
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        reponse = "",
                        error = "Échec de la suppression des événements dans le calendrier."
                    });

                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"CalendarDeleteTool.ExecuteAsync : {ex.Message}");
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
                    Description = "Chaine de caractère cherchée dans le titre des événements ou tâches",
                    Required = false
                },
                new ToolParameter
                {
                    Name = "timemin",
                    Type = "string",
                    Description = "Date et heure minimum pour borner la recherche d'événements ou de tâches dans le calendrier (format : yyyy-MM-dd HH:mm)",
                    Required = false
                },
                new ToolParameter
                {
                    Name = "timemax",
                    Type = "string",
                    Description = "Date et heure maximum pour borner la recherche d'événements ou de tâches dans le calendrier (format : yyyy-MM-dd HH:mm)",
                    Required = false
                }
            };
        }
    }
    #endregion

    #region Tool CalendarSearch
    public class CalendarSearchTool : ITool
    {
        public string Name => "calendar_search";
        public string Description => "Outil pour chercher des événements dans le calendrier.";

        private ICalendarService _Service;
        private string _TimeZone;

        public CalendarSearchTool(string url, string login, string password, string timeZone)
        {
            LoggerService.LogInfo("CalendarSearchTool");
            _Service = new CalendarCalDavService(url, login, password);
            _TimeZone = timeZone;
        }

        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            LoggerService.LogInfo("CalendarSearchTool.ExecuteAsync");

            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(_TimeZone);

                DateTimeOffset timeMin;
                DateTimeOffset timeMax;

                var query = parameters.ContainsKey("query")
                    ? parameters["query"].ToString()
                    : "";

                if (parameters.TryGetValue("timemin", out var rawMin) && rawMin is string s1
                    && DateTime.TryParseExact(s1, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtMin))
                {
                    var offset = tz.GetUtcOffset(dtMin);
                    timeMin = new DateTimeOffset(dtMin, offset);
                }
                else
                {
                    timeMin = DateTimeOffset.UtcNow.AddDays(-7); // défaut : 7 jours dans le passé
                }

                if (parameters.TryGetValue("timemax", out var rawMax) && rawMax is string s2
                    && DateTime.TryParseExact(s2, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtMax))
                {
                    var offset = tz.GetUtcOffset(dtMax);
                    timeMax = new DateTimeOffset(dtMax, offset);
                }
                else
                {
                    timeMax = DateTimeOffset.UtcNow.AddDays(+7); // défaut : 7 jours dans le futur
                }


                var (bOk, lst) = await _Service.SearchAsync(timeMin, timeMax, query);
                if (!bOk)
                {
                    LoggerService.LogWarning($"CalendarSearchTool.ExecuteAsync : Échec de l'exécution de recherche dans le calendrier");
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        reponse = "",
                        error = "Échec de l'exécution de recherche dans le calendrier."
                    });
                }
                if (lst == null || lst.Count == 0)
                {
                    LoggerService.LogWarning($"CalendarSearchTool.ExecuteAsync : Aucun élément n'a été trouvé avec les critères indiqués");
                    return JsonService.Serialize(new
                    {
                        ok = true,
                        reponse = "Aucun élément n'a été trouvé avec les critères indiqués.",
                        error = ""
                    });
                }

                LoggerService.LogDebug($"CalendarSearchTool.ExecuteAsync: Evenements trouvés --> {lst.Count}");

                var localMin = TimeZoneInfo.ConvertTime(timeMin, tz);
                var localMax = TimeZoneInfo.ConvertTime(timeMax, tz);

                StringBuilder sb = new();
                sb.AppendLine($"Voici les {lst.Count} Événements trouvés entre {localMin:O} et {localMax:O} :");

                foreach (var ev in lst)
                {
                    var localStart = TimeZoneInfo.ConvertTime(ev.StartUtc, tz); 

                    var lstAttendees = ev.Attendees != null && ev.Attendees.Count > 1
                        ? $" - (Invités: {string.Join(", ", ev.Attendees)})"
                        : "";
                    lstAttendees = lstAttendees.Replace("mailto:", "",StringComparison.InvariantCultureIgnoreCase);
                    //sb.AppendLine($"Evenement: {ev.Summary} - {ev.localStart:O} {lstAttendees}");
                    sb.AppendLine($"Evenement: {ev.Summary} - {localStart:O}");
                }

                return JsonService.Serialize(new
                {
                    ok = true,
                    reponse = sb.ToString(),
                    error = ""
                });

            }

            catch (Exception ex)
            {
                LoggerService.LogError($"CalendarSearchTool.ExecuteAsync : {ex.Message}");
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
                    Description = "Chaine de caractère cherchée dans le titre des événements ou tâches",
                    Required = false
                },
                new ToolParameter
                {
                    Name = "timemin",
                    Type = "string",
                    Description = "Date et heure minimum pour borner la recherche d'événements ou de tâches dans le calendrier (format : yyyy-MM-dd HH:mm)",
                    Required = false
                },
                new ToolParameter
                {
                    Name = "timemax",
                    Type = "string",
                    Description = "Date et heure maximum pour borner la recherche d'événements ou de tâches dans le calendrier (format : yyyy-MM-dd HH:mm)",
                    Required = false
                }
            };
        }
    }
    #endregion

    #region Interface (en cas de changement de type de calendrier)
    public interface ICalendarService
    {
        Task<(bool, IReadOnlyList<CalendarEventDto>)> SearchAsync(DateTimeOffset timeMinUtc, DateTimeOffset timeMaxUtc, string? query = null, int maxResults = 50);
        Task<(bool, CreateEventResult)> CreateEventAsync(CreateEventRequest req, CancellationToken ct = default);
        Task<bool> DeleteEventsAsync(List<string> lstresourceUrl, string? etag = null, CancellationToken ct = default);
    }
    #endregion

    #region CalendarCalDavService
    public class CalendarCalDavService: ICalendarService
    {
        #region Variables
        private static readonly XNamespace DavNs = "DAV:";
        private static readonly XNamespace CalDavNs = "urn:ietf:params:xml:ns:caldav";

        private readonly HttpClient _http;
        private readonly CalDavOptions _opt;
        #endregion

        #region Constructeur
        public CalendarCalDavService(string url, string login, string password)
        {
            LoggerService.LogInfo($"CalendarService");
            _opt = new CalDavOptions
            {
                CalendarUrl = new Uri(url),
                Username = login,
                AppPassword = password
            };

            _http = new HttpClient();
            _http.Timeout = _opt.Timeout;

            // Auth Basic : user:appPassword
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_opt.Username}:{_opt.AppPassword}"));
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);

            // CalDAV / WebDAV
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("CalendarSearchService/1.0");
            _http.DefaultRequestHeaders.Accept.ParseAdd("application/xml");
        }
        #endregion

        #region Méthodes publiques
        /// <summary>
        /// Recherche les événements qui chevauchent l’intervalle [timeMinUtc, timeMaxUtc]
        /// (CalDAV time-range filtre sur DTSTART/DTEND selon implémentation, Nextcloud répond bien.)
        /// </summary>
        public async Task<(bool, IReadOnlyList<CalendarEventDto>)> SearchAsync(DateTimeOffset timeMinUtc, DateTimeOffset timeMaxUtc, string? query = null, int maxResults = 50)
        {
            LoggerService.LogInfo($"CalendarService.SearchAsync: timeMin={timeMinUtc.DateTime.ToString("dd/MM/yyyy HH:mm")}, timeMax={timeMaxUtc.DateTime.ToString("dd/MM/yyyy HH:mm")}, query='{query}'");
            try
            {
                if (timeMaxUtc < timeMinUtc)
                {
                    var v1 = timeMinUtc;
                    var v2 = timeMaxUtc;
                    timeMaxUtc = v1;
                    timeMinUtc = v2;
                }

                // clamp
                maxResults = Math.Clamp(maxResults, 1, Math.Min(_opt.MaxResultsHardLimit, 500));

                // On ramene tous ceux de la journée et on filtre ensuite sur les heures
                DateTimeOffset min = timeMinUtc.Date;
                DateTimeOffset max = timeMaxUtc.Date.AddDays(1).AddSeconds(-1);

                //var reportBody = BuildCalendarQueryReport(timeMinUtc, timeMaxUtc);
                var reportBody = BuildCalendarQueryReport(min, max);

                using var req = new HttpRequestMessage(new HttpMethod("REPORT"), _opt.CalendarUrl)
                {
                    Content = new StringContent(reportBody, Encoding.UTF8, "application/xml")
                };

                // Profondeur: 1 pour inclure les ressources dans le calendrier
                req.Headers.Add("Depth", "1");

                using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                var xml = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    LoggerService.LogError($"CalDAV REPORT failed: {(int)resp.StatusCode} {resp.ReasonPhrase}\n{xml}");
                    return (false, Array.Empty<CalendarEventDto>());
                }

                var items = ParseMultistatusCalendarData(xml, _opt.CalendarUrl);

                // Parse ICS -> events
                var results = new List<CalendarEventDto>(capacity: Math.Min(items.Count, maxResults));
                foreach (var item in items)
                {
                    if (results.Count >= maxResults) 
                        break;

                    if (string.IsNullOrWhiteSpace(item.Ics)) 
                        continue;

                    // Sert également à filtrer sur les heures
                    var lst = ExtractEventsFromIcs(item.Ics, item.Href, timeMinUtc, timeMaxUtc);
                    foreach (var ev in lst)
                    {
                        if (results.Count >= maxResults) 
                            break;
                        
                        results.Add(ev);
                    }
                }

                // Filtrage "query" côté client (Nextcloud ne fait pas bien du full-text search CalDAV)
                if (!string.IsNullOrWhiteSpace(query))
                {
                    var q = query.Trim();
                    results = results
                        .Where(e =>
                            (e.Summary?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                            (e.Description?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                            (e.Location?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
                        .ToList();
                }

                // tri chrono
                return (true, results
                    .OrderBy(e => e.StartUtc)
                    .Take(maxResults)
                    .ToList());
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"CalendarService.SearchAsync : {ex}");
                return (false, Array.Empty<CalendarEventDto>());
            }
        }

        /// <summary>
        /// Création d’un événement dans le calendrier via CalDAV PUT (ressource .ics)
        /// </summary>
        /// <param name="req"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async Task<(bool, CreateEventResult)> CreateEventAsync(CreateEventRequest req, CancellationToken ct = default)
        {
            LoggerService.LogInfo($"CalendarService.CreateEventAsync");

            try
            {
                if (req is null)
                    throw new ArgumentNullException(nameof(req));

                if (string.IsNullOrWhiteSpace(req.Title))
                    throw new ArgumentException("Le titre est obligatoire.");

                var d1 = req.Start;
                var d2 = req.End;
                if (req.End < req.Start)
                {
                    d1 = req.End;
                    d2 = req.Start;
                }

                // UID stable et unique
                var uid = Guid.NewGuid().ToString("N");

                // On stocke en UTC (recommandé pour éviter les bugs DST)
                var startUtc = d1.ToUniversalTime();
                var endUtc = d2.ToUniversalTime();

                var ics = BuildVEventIcs(
                    uid: uid,
                    title: req.Title.Trim(),
                    startUtc: startUtc,
                    endUtc: endUtc,
                    location: req.Location,
                    description: req.Description,
                    attendeesEmails: req.AttendeesEmails,
                    rrule: req.Rrule
                );

                // URL de la ressource .ics
                var resourceUrl = new Uri(_opt.CalendarUrl, $"{uid}.ics");

                using var httpReq = new HttpRequestMessage(HttpMethod.Put, resourceUrl)
                {
                    Content = new StringContent(ics, Encoding.UTF8, "text/calendar")
                };
                httpReq.Content.Headers.ContentType!.CharSet = "utf-8";

                // Optionnel mais souvent apprécié
                httpReq.Headers.TryAddWithoutValidation("If-None-Match", "*");

                using var resp = await _http.SendAsync(httpReq, HttpCompletionOption.ResponseHeadersRead, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"CalDAV PUT failed: {(int)resp.StatusCode} {resp.ReasonPhrase}\n{body}");
                }

                // ETag utile si tu veux faire update ensuite avec If-Match
                var etag = resp.Headers.ETag?.Tag;

                return (true, new CreateEventResult
                {
                    Uid = uid,
                    ResourceUrl = resourceUrl,
                    ETag = etag
                });
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"CalendarService.CreateEventAsync : {ex}");
                var id = Guid.NewGuid().ToString();
                return (false, new CreateEventResult
                {
                    Uid = id,
                    ResourceUrl = new Uri(_opt.CalendarUrl, $"{id}.ics"),
                    ETag = ""
                });
            }
        }

        /// <summary>
        /// Suppression de plusieurs événements dans le calendrier via CalDAV DELETE
        /// </summary>
        /// <param name="lstresourceUrl"></param>
        /// <param name="etag"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="HttpRequestException"></exception>
        public async Task<bool> DeleteEventsAsync(List<string> lstreSourceUrl, string? etag = null, CancellationToken ct = default)
        {
            try
            {
                if (lstreSourceUrl is null)
                    throw new ArgumentNullException(nameof(lstreSourceUrl));

                foreach (var resourceUrl in lstreSourceUrl)
                {
                    var url = new Uri(_opt.CalendarUrl, $"{resourceUrl}.ics");
                    using var request = new HttpRequestMessage(HttpMethod.Delete, url);

                    // Sécurité : si tu as un ETag, protège-toi contre l’écrasement concurrent
                    if (!string.IsNullOrWhiteSpace(etag))
                    {
                        request.Headers.TryAddWithoutValidation("If-Match", etag);
                    }

                    using var response = await _http.SendAsync(request, ct);

                    if (!response.IsSuccessStatusCode)
                    {
                        var body = await response.Content.ReadAsStringAsync(ct);
                        throw new HttpRequestException(
                            $"CalDAV DELETE failed: {(int)response.StatusCode} {response.ReasonPhrase}\n{body}");
                    }

                }
                return true;
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"CalendarService.DeleteEventsAsync : {ex}");
                return false;
            }
        }
        #endregion

        #region Méthodes privées

        #region Creation
        private static string BuildVEventIcs(string uid, string title, DateTimeOffset startUtc, DateTimeOffset endUtc, string? location, string? description, List<string>? attendeesEmails, string? rrule)
        {
            static string UtcStamp(DateTimeOffset dto) => dto.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'");

            static string EscapeText(string? s)
            {
                if (string.IsNullOrEmpty(s)) return "";
                // RFC5545 TEXT escaping: backslash, comma, semicolon, newline
                return s.Replace(@"\", @"\\")
                        .Replace(";", @"\;")
                        .Replace(",", @"\,")
                        .Replace("\r\n", @"\n")
                        .Replace("\n", @"\n");
            }

            var dtstamp = UtcStamp(DateTimeOffset.UtcNow);
            var dtstart = UtcStamp(startUtc);
            var dtend = UtcStamp(endUtc);

            var sb = new StringBuilder();
            sb.AppendLine("BEGIN:VCALENDAR");
            sb.AppendLine("VERSION:2.0");
            sb.AppendLine("PRODID:-//YourCompany//YourApp//FR");
            sb.AppendLine("CALSCALE:GREGORIAN");
            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine($"UID:{uid}");
            sb.AppendLine($"DTSTAMP:{dtstamp}");
            sb.AppendLine($"DTSTART:{dtstart}");
            sb.AppendLine($"DTEND:{dtend}");
            sb.AppendLine($"SUMMARY:{EscapeText(title)}");

            if (!string.IsNullOrWhiteSpace(location))
                sb.AppendLine($"LOCATION:{EscapeText(location)}");

            if (!string.IsNullOrWhiteSpace(description))
                sb.AppendLine($"DESCRIPTION:{EscapeText(description)}");

            if (!string.IsNullOrWhiteSpace(rrule))
                sb.AppendLine($"RRULE:{rrule}");

            // Attendees (optionnel). Pour Nextcloud/Thunderbird, ça passe, mais l’acceptation/RSVP dépend du client.
            if (attendeesEmails != null && attendeesEmails.Count > 0)
            {
                foreach (var email in attendeesEmails.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()))
                {
                    // Version simple. Tu peux enrichir avec CN=Nom, RSVP=TRUE, ROLE=REQ-PARTICIPANT, etc.
                    sb.AppendLine($"ATTENDEE;CN={EscapeText(email)}:mailto:{email}");
                }
            }

            sb.AppendLine("END:VEVENT");
            sb.AppendLine("END:VCALENDAR");

            return sb.ToString();
        }

        #endregion

        #region Recherche
        private static string BuildCalendarQueryReport(DateTimeOffset minUtc, DateTimeOffset maxUtc)
        {
            // CalDAV time-range attend un format "YYYYMMDDTHHMMSSZ"
            static string ToCalDavUtc(DateTimeOffset dt) =>
                dt.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'");

            var start = ToCalDavUtc(minUtc);
            var end = ToCalDavUtc(maxUtc);

            // On demande:
            // - href (dans multistatus)
            // - getetag (optionnel)
            // - calendar-data (le contenu ICS)
            // Et on filtre par time-range sur VEVENT
            return $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <c:calendar-query xmlns:d="DAV:" xmlns:c="urn:ietf:params:xml:ns:caldav">
                  <d:prop>
                    <d:getetag/>
                    <c:calendar-data/>
                  </d:prop>
                  <c:filter>
                    <c:comp-filter name="VCALENDAR">
                      <c:comp-filter name="VEVENT">
                        <c:time-range start="{start}" end="{end}"/>
                      </c:comp-filter>
                    </c:comp-filter>
                  </c:filter>
                </c:calendar-query>
                """;
        }

        private sealed class CalendarDataItem
        {
            public required Uri Href { get; init; }
            public string? Ics { get; init; }
        }

        private static List<CalendarDataItem> ParseMultistatusCalendarData(string xml, Uri baseCalendarUrl)
        {
            var doc = XDocument.Parse(xml);

            // DAV:multistatus / DAV:response
            var responses = doc.Root?
                .Elements(DavNs + "response")
                .ToList() ?? new List<XElement>();

            var items = new List<CalendarDataItem>();

            foreach (var r in responses)
            {
                var hrefText = r.Element(DavNs + "href")?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(hrefText)) continue;

                // href est souvent relatif (/remote.php/dav/...)
                // on reconstruit en Uri absolue
                var href = Uri.TryCreate(hrefText, UriKind.Absolute, out var abs)
                    ? abs
                    : new Uri(baseCalendarUrl, hrefText);

                // propstat/prop/calendar-data
                var calendarData = r
                    .Elements(DavNs + "propstat")
                    .Select(ps => ps.Element(DavNs + "prop"))
                    .Where(p => p != null)
                    .Select(p => p!.Element(CalDavNs + "calendar-data")?.Value)
                    .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

                if (!string.IsNullOrWhiteSpace(calendarData))
                {
                    items.Add(new CalendarDataItem { Href = href, Ics = calendarData });
                }
            }

            return items;
        }

        private static IEnumerable<CalendarEventDto> ExtractEventsFromIcs(string ics, Uri href, DateTimeOffset minUtc, DateTimeOffset maxUtc)
        {
            if (string.IsNullOrWhiteSpace(ics))
                yield break;

            if (maxUtc <= minUtc)
                yield break;

            Ical.Net.Calendar? cal;
            try
            {
                cal = Ical.Net.Calendar.Load(ics);
            }
            catch
            {
                yield break; // ICS invalide
            }

            if(cal == null) 
                yield break;

            // Fenêtre en CalDateTime UTC (important)
            var winStart = new CalDateTime(minUtc.UtcDateTime, "UTC");
            var winEnd = new CalDateTime(maxUtc.UtcDateTime, "UTC");


            foreach (var e in cal.Events)
            {
                // Dans ta version, GetOccurrences attend (start, options?) -> on ne passe QUE start.
                // Ensuite on borne à la main sur winEnd.
                IEnumerable<Occurrence> occs;
                try
                {
                    occs = e.GetOccurrences(winStart); // ✅ compatible avec la signature que tu as
                }
                catch
                {
                    continue; // un event mal fichu ne doit pas casser toute la recherche
                }

                foreach (var occ in occs)
                {
                    // Filtre "end window" : dès qu’on dépasse, on arrête (les occurrences sont généralement chronologiques)
                    // Si jamais ce n’est pas trié (rare), ça reste correct mais sans le break.
                    if (occ?.Period?.StartTime == null)
                        continue;

                    var occStartCal = occ.Period.StartTime;
                    if (occStartCal.CompareTo(winEnd) >= 0)
                        break;

                    // On récupère start/end en UTC
                    var startUtc = ToUtcOffset(occ.Period.StartTime);
                    var endUtc = occ.Period.EffectiveEndTime != null
                        ? ToUtcOffset(occ.Period.EffectiveEndTime)
                        : startUtc;

                    // Filtre de chevauchement sur la fenêtre demandée
                    if (endUtc <= minUtc || startUtc >= maxUtc)
                        continue;

                    yield return new CalendarEventDto
                    {
                        Uid = e.Uid,
                        Summary = e.Summary,
                        Description = e.Description,
                        Location = e.Location,
                        StartUtc = startUtc,
                        EndUtc = endUtc,
                        Href = href,
                        Attendees = (e.Attendees ?? new List<Attendee>())
                            .Select(a => a.Value?.ToString() ?? "")
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .ToList()
                    };
                }
            }

            static DateTimeOffset ToUtcOffset(CalDateTime cdt)
            {
                // AsUtc renvoie un DateTime ; on l’ancre en UTC si nécessaire
                var dt = cdt.AsUtc;
                if (dt.Kind == DateTimeKind.Unspecified)
                    dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);

                return new DateTimeOffset(dt);
            }
        }
        #endregion

        #endregion
    }
    #endregion

    #region Classes annexes
    public sealed class CalDavOptions
    {
        /// <summary>
        /// Exemple: https://tonserveur/remote.php/dav/calendars/USER/CALENDAR_NAME/
        /// </summary>
        public required Uri CalendarUrl { get; init; }

        /// <summary>Nextcloud username</summary>
        public required string Username { get; init; }

        /// <summary>Nextcloud App Password (recommandé)</summary>
        public required string AppPassword { get; init; }

        /// <summary>Timeout HTTP</summary>
        public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

        /// <summary>Filtre de sécurité: limite max évènements renvoyés</summary>
        public int MaxResultsHardLimit { get; init; } = 500;
    }

    public sealed class CalendarEventDto
    {
        public string? Uid { get; init; }
        public string? Summary { get; init; }
        public string? Description { get; init; }
        public string? Location { get; init; }

        /// <summary>Heure de début en UTC</summary>
        public DateTimeOffset StartUtc { get; init; }

        /// <summary>Heure de fin en UTC</summary>
        public DateTimeOffset EndUtc { get; init; }

        /// <summary>URL CalDAV de la ressource .ics (utile pour read/update/delete)</summary>
        public Uri? Href { get; init; }
        public List<string> Attendees { get; init; } = new List<string>();
    }
    public sealed class CreateEventResult
    {
        /// <summary>
        /// UID iCalendar (clé logique de l’événement)
        /// </summary>
        public required string Uid { get; init; }

        /// <summary>
        /// URL complète de la ressource .ics créée
        /// </summary>
        public required Uri ResourceUrl { get; init; }

        /// <summary>
        /// ETag retourné par Nextcloud (utile pour update avec If-Match)
        /// </summary>
        public string? ETag { get; init; }
    }
    public sealed class CreateEventRequest
    {
        public required string Title { get; init; }
        public required DateTimeOffset Start { get; init; }
        public required DateTimeOffset End { get; init; }
        public string? Location { get; init; }
        public string? Description { get; init; }
        public List<string>? AttendeesEmails { get; init; }
        public string? Rrule { get; init; }
    }
    #endregion
}
