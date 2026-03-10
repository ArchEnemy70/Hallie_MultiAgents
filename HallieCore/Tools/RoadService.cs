using ExternalServices;
using HallieDomain;
using System.Text;
using System.Text.Json;

namespace Hallie.Tools
{
    #region Tool
    public class RoadTool : ITool
    {
        public string Name => "road_route";
        public string Description =>
            "Calcule un itinéraire entre un point de départ et un point d'arrivée. " +
            "Retourne un texte résumant le trajet (distance, durée, étapes intermédiaires) " +
            "et génère un fichier HTML affichant la carte interactive. " +
            "Exemple de sortie textuelle attendue :\n" +
            "Voici le trajet de [Départ] à [Arrivée] :\n" +
            "Distance : 12 km\n" +
            "Durée estimée : 25 minutes\n" +
            "Étapes : Ville1, Ville2, Ville3\n" +
            "Le fichier HTML du trajet est généré et peut être ouvert dans un navigateur.";

        private readonly RoadService _service;

        public RoadTool()
        {
            LoggerService.LogInfo("RoadTool");
            var service = new RoadService();
            _service = service;
        }

        public RoadTool(RoadService service)
        {
            LoggerService.LogInfo("RoadTool");

            _service = service;
        }

        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            LoggerService.LogInfo("RoadTool.ExecuteAsync");


            try
            {
                var depart = parameters["depart"].ToString() ?? "";
                var arrivee = parameters["arrivee"].ToString() ?? "";


                if (string.IsNullOrWhiteSpace(depart))
                    return "Erreur : Le point de départ ne peut pas être vide";
                if (string.IsNullOrWhiteSpace(arrivee))
                    return "Erreur : Le point d'arrivée ne peut pas être vide";

                // Rechercher dans le service RoadService
                var results = await _service.GetRoad(depart, arrivee);
                if (results == null)
                {
                    return "Erreur : Aucun résultat trouvé pour l'itinéraire demandé.";
                }

                // Formater les résultats pour le LLM
                var context = new StringBuilder();
                context.AppendLine($"Voici le trajet de {results.DepartGeocode!.Name} {results.DepartGeocode.Meteo} à {results.ArriveeGeocode!.Name} {results.ArriveeGeocode!.Meteo} :");
                context.AppendLine($"La distance est de {results.Distances[0]} kilometres");
                if (results.Durees[0] < 60)
                {
                    context.AppendLine($"Le temps estimé est de {results.Durees[0]} minutes");
                }
                else
                {
                    var hours = results.Durees[0] / 60;
                    var minutes = results.Durees[0] % 60;
                    context.AppendLine($"Le temps estimé est de {hours} heures et {minutes} minutes");
                }

                context.AppendLine($"Les étapes (et la météo qu'il faut indiquer) sont :");
                foreach(var s in results.Etapes[0])
                {
                    context.AppendLine($"{s.Name} {s.Meteo}, ");
                }
                if(results.NbrRoute > 1)
                {
                    context.AppendLine($"Il faut préciser qu'il y a {results.NbrRoute} itinéraires possibles.");
                }
                context.AppendLine($"");

                // On affiche la carte dans le navigateur
                var filename = "road.html";
                string filePath = System.IO.Path.Combine(Environment.CurrentDirectory, filename);
                var b = TxtService.CreateTextFile(filePath, results.HtmlMap);
                if(!b)
                {
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        reponse = "",
                        error = "Une erreur s'est produite"
                    });
                }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });

                // On retourne le contexte textuel
                return JsonService.Serialize(new
                {
                    ok = true,
                    reponse = context.ToString(),
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
                    Name = "depart",
                    Type = "string",
                    Description = "Le point de départ pour le trajet",
                    Required = true
                },
                new ToolParameter
                {
                    Name = "arrivee",
                    Type = "string",
                    Description = "Le point d'arrivée pour le trajet",
                    Required = true
                }
            };
        }
    }
    #endregion

    #region Service
    public class RoadService
    {
        private WeatherApiComService _WeatherService = new WeatherApiComService(Params.WeatherUrl!, Params.WeatherApiKey!);

        #region Méthode publique
        public async Task<RoadReponse?> GetRoad(string depart, string arrivee)
        {
            try
            {
                LoggerService.LogDebug("RoadService.GetRoad");

                var start = await GeocodeAsync(depart);
                var end = await GeocodeAsync(arrivee);

                var routes = await GetRouteAsync(start, end);
                var durees = routes.Select(r => r.Duration / 60).ToList();
                durees = durees.Select(r => Math.Round(r, 1)).ToList();
                var distances = routes.Select(r => r.Distance / 1000).ToList();
                distances = distances.Select(r => Math.Round(r, 1)).ToList();

                var etapess = new List<List<GeoPointN>>();
                foreach (var r in routes)
                {
                    var cities = await GetCitiesAlongRouteAsync(r);
                    etapess.Add(cities);
                }

                //var cities = await GetCitiesAlongRouteAsync(route);

                var htmlMap = GetHtmlRoad(start, end, etapess);
                // var listCityes = string.Join(", ", cities.Select(c => c.Name));
                var response = new RoadReponse
                {
                    Depart = depart,
                    DepartGeocode = start,
                    Arrivee = arrivee,
                    ArriveeGeocode = end,
                    Durees = durees,
                    Distances = distances,
                    Etapes = etapess,
                    HtmlMap = htmlMap,
                    NbrRoute = routes.Count
                };

                return response;
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"RoadService.GetRoad Exception : {ex.Message}");
                return null;
            }
        }
        #endregion

        #region Méthodes privées
        private async Task<GeoPoint> GeocodeAsync(string address)
        {
            LoggerService.LogDebug($"RoadService.GeocodeAsync : {address}");

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("GeoApp/1.0");

            var url =
                $"https://nominatim.openstreetmap.org/search" +
                $"?q={Uri.EscapeDataString(address)}" +
                $"&format=json&limit=1";

            var json = await client.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            var result = doc.RootElement[0];
            var meteo = "";
            var weather = await _WeatherService.GetWeatherAsync(address);
            int weatherId = 0;
            if (weather != null)
            {
                meteo = $"({weather!.Description}, {weather!.Temperature}°c)";
                weatherId = weather.WeatherId;
            }
            var name = result.GetProperty("display_name").GetString()!;
            var lat = result.GetProperty("lat").GetString()!;
            var lon = result.GetProperty("lon").GetString()!;

            return new GeoPoint(name, lat, lon, meteo, weatherId);
        }

        private async Task<List<RouteGeometry>> GetRouteAsync(GeoPoint start, GeoPoint end)
        {
            LoggerService.LogDebug($"RoadService.GetRouteAsync : {start.Name} - {end.Name}");

            using var client = new HttpClient();

            var url =
                $"https://router.project-osrm.org/route/v1/driving/" +
                $"{start.Lon},{start.Lat};{end.Lon},{end.Lat}" +
                $"?alternatives=true&overview=full&geometries=geojson";

            var json = await client.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            var nbRoutes =  doc.RootElement.GetProperty("routes").GetArrayLength();

            var routes = new List<RouteGeometry>();


            for (int i = 0;i<nbRoutes;i++)
            {
                var coords = doc.RootElement
                    .GetProperty("routes")[i]
                    .GetProperty("geometry")
                    .GetProperty("coordinates");

                var duration = doc.RootElement
                    .GetProperty("routes")[i]
                    .GetProperty("duration").GetDouble();

                var distance = doc.RootElement
                    .GetProperty("routes")[i]
                    .GetProperty("distance").GetDouble();


                var points = coords
                    .EnumerateArray()
                    .Select(c => new GeoPointN(
                        Name: "",
                        Meteo: "",
                        MeteoId: 0,
                        Lat: c[1].GetDouble()!,
                        Lon: c[0].GetDouble()!
                        ))
                    .ToList();

                routes.Add(new RouteGeometry(distance, duration, points));
            }



            return routes;
        }

        private async Task<string?> GetCityAsync(GeoPointN p)
        {
            LoggerService.LogDebug($"RoadService.GetCityAsync");

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("GeoApp/1.0");

            var pLat = p.Lat.ToString().Replace(",", ".");
            var pLon = p.Lon.ToString().Replace(",", ".");

            var url =
                $"https://nominatim.openstreetmap.org/reverse" +
                $"?lat={pLat}&lon={pLon}&format=json&addressdetails=1";

            var json = await client.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("address", out var addr))
                return null;

            string[] keys = { "city", "town", "village", "municipality" };

            foreach (var k in keys)
                if (addr.TryGetProperty(k, out var v))
                    return v.GetString();

            return null;
        }

        private async Task<List<GeoPointN>> GetCitiesAlongRouteAsync(RouteGeometry route)
        {
            LoggerService.LogDebug($"RoadService.GetCitiesAlongRouteAsync : {route.Points.Count} points");


            var cities = new List<GeoPointN>();
            string? lastCity = null;

            // 1 point tous les ~250 points (à ajuster)
            int nbPointsStep = 12;
            int step = Math.Max(12, route.Points.Count / nbPointsStep);

            for (int i = step; i < route.Points.Count; i += step)
            {
                var cityName = await GetCityAsync(route.Points[i]);
                if (cityName != null && cityName != lastCity)
                {
                    var weather = await _WeatherService.GetWeatherAsync(cityName);
                    var meteo = "";
                    var meteoId = 0;
                    if (weather != null)
                    {
                        meteo = $"({weather!.Description}, {weather!.Temperature}°c)";
                        meteoId = weather.WeatherId;
                    }
                    var cityPoint = route.Points[i] with { Name = cityName, Meteo = meteo, MeteoId = meteoId };
                    cities.Add(cityPoint);
                    lastCity = cityName;
                }
            }

            var last = route.Points.Count - 1;
            var cityNameLast = await GetCityAsync(route.Points[last]);
            if (cityNameLast != null && cityNameLast != lastCity)
            {
                var weather = await _WeatherService.GetWeatherAsync(cityNameLast);
                var meteo = "";
                var meteoId = 0;
                if (weather != null)
                {
                    meteo = $"({weather!.Description}, {weather!.Temperature}°c)";
                    meteoId = weather.WeatherId;
                }
                var cityPoint = route.Points[last] with { Name = cityNameLast, Meteo = meteo, MeteoId = meteoId };
                cities.Add(cityPoint);
                lastCity = cityNameLast;
            }

            return cities;
        }

        private string GetHtmlEtapes(List<List<GeoPointN>> etapes)
        {
            LoggerService.LogDebug($"RoadService.GetHtmlEtapes : {etapes[0].Count} étapes");

            var html = "";

            for (int j = 0; j < etapes.Count; j++)
            {
                var f = etapes[j];
                for (int i = 0; i < f.Count; i++)
                {
                    var e = f[i];
                    html += $$"""{ name: "{{e.Name}} {{e.Meteo}}", lat: {{e.Lat.ToString().Replace(",", ".")}}, lon: {{e.Lon.ToString().Replace(",", ".")}}, id: {{e.MeteoId}} },""";
                }
            }

            return html;

        }

        private string GetHtmlRoad(GeoPoint departGeocode, GeoPoint arriveeGeocode, List<List<GeoPointN>> etapes)
        {
            LoggerService.LogDebug($"RoadService.GetHtmlRoad");

            var etapesHtml = GetHtmlEtapes(etapes);

            return $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8" />
                <title>Carte routière (OSRM + Leaflet)</title>

                <link
                rel="stylesheet"
                href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css"
                />
                <script
                src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js">
                </script>

                <style>
                    html, body, #map { height: 100%; margin: 0; }
                    #info {
                        position: absolute;
                        top: 10px;
                        left: 50%;
                        transform: translateX(-50%);
                        background: white;
                        padding: 8px 15px;
                        border-radius: 5px;
                        font-family: sans-serif;
                        font-weight: bold;
                        z-index: 1000;
                        box-shadow: 0 0 5px rgba(0,0,0,0.3);
                    }
                </style>
            </head>

            <body>
                <div id="info">Chargement de l'itinéraire...</div>
                <div id="map"></div>

                <script>
                // Waypoints (lon, lat)
                const start = [{{departGeocode.Lon}}, {{departGeocode.Lat}}];
                const end   = [{{arriveeGeocode.Lon}}, {{arriveeGeocode.Lat}}];

                // Carte
                const map = L.map('map').setView([{{departGeocode.Lat}}, {{departGeocode.Lon}}], 14);

                L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                    attribution: '© OpenStreetMap contributors'
                }).addTo(map);

                // Marqueurs
                L.circleMarker([start[1], start[0]], {
                    radius: 8,
                    color: "green",
                    fillOpacity: 0.8
                    }).addTo(map).bindPopup("Départ : {{departGeocode.Name}} {{departGeocode.Meteo}}");

                L.circleMarker([end[1], end[0]], {
                    radius: 8,
                    color: "red",
                    fillOpacity: 0.8
                    }).addTo(map).bindPopup("Arrivée : {{arriveeGeocode.Name}} {{arriveeGeocode.Meteo}}");

                function getIconByMeteoId(id) {
                    let iconUrl;
                    if (id >= 200 && id <= 232) iconUrl = "icons/storm.png";      // Orage
                    else if (id >= 300 && id <= 321) iconUrl = "icons/drizzle.png"; // Bruine
                    else if (id >= 500 && id <= 531) iconUrl = "icons/rain.png";    // Pluie
                    else if (id >= 600 && id <= 622) iconUrl = "icons/snow.png";    // Neige
                    else if (id >= 701 && id <= 781) iconUrl = "icons/fog.png";     // Atmosphère
                    else if (id === 800) iconUrl = "icons/sun.png";                 // Ciel dégagé
                    else if (id >= 801 && id <= 804) iconUrl = "icons/cloud.png";   // Nuages
                    else iconUrl = "icons/default.png";
            
                    return L.icon({
                        iconUrl: iconUrl,
                        iconSize: [40, 40],
                        iconAnchor: [15, 30],
                        popupAnchor: [0, -30]
                    });
                }

                // Appel OSRM
                const url = `https://router.project-osrm.org/route/v1/driving/` +
                            `${start[0]},${start[1]};${end[0]},${end[1]}` +
                            `?alternatives=true&overview=full&geometries=geojson`;

                fetch(url)
                    .then(r => r.json())
                    .then(data => {
                    const route = data.routes[0].geometry;

                    // Tracé de la route
                    let routeLine; // pour fitBounds
                    data.routes.forEach((r, index) => {
                        const line = L.geoJSON(r.geometry, {
                            style: {
                                color: index === 0 ? "blue" : "purple",
                                weight: index === 0 ? 5 : 3,
                                opacity: 0.7
                            }
                        }).addTo(map);

                        if (index === 0) routeLine = line; // on garde la première route pour fitBounds
                    });

                    // Infos distance / durée
                    const distKm = (data.routes[0].distance / 1000).toFixed(1);
                    let durMin = Math.round(data.routes[0].duration / 60);
                    let durText;
                    if(durMin >= 60){
                        const hours = Math.floor(durMin / 60);
                        const minutes = durMin % 60;
                        durText = `${hours}h ${minutes}min`;
                    } else {
                        durText = `${durMin} min`;
                    }
                    document.getElementById("info").innerText = `Distance : ${distKm} km - Durée : ${durText}`;

                    const steps = [
                        {{etapesHtml}}
                    ];

                    steps.forEach((step) => {
                        const marker = L.marker([step.lat, step.lon], {
                            icon: getIconByMeteoId(step.id)
                        }).addTo(map);

                        marker.bindPopup(`<b>${step.name}</b>`);
                    });



                    // Ajuste le zoom à la route
                    if (routeLine) map.fitBounds(routeLine.getBounds());
                });


                    
            
                </script>
            </body>
            </html>
                
            """;
        }
        #endregion
    }
    #endregion

    #region Models et classes annexes
    public record GeoPointN(string Name, double Lat, double Lon, string Meteo, int MeteoId);

    public record GeoPoint(string Name, string Lat, string Lon, string Meteo, int MeteoId);

    public record RouteGeometry(double Distance, double Duration, List<GeoPointN> Points);

    public class RoadReponse
    {
        public string Depart { get; set; } = "";
        public GeoPoint? DepartGeocode { get; set; }
        public string Arrivee { get; set; } = "";
        public GeoPoint? ArriveeGeocode { get; set; }
        public List<double> Durees { get; set; } = new();
        public List<double> Distances { get; set; } = new();
        public List<List<GeoPointN>> Etapes { get; set; } = new();
        public string HtmlMap { get; set; } = "";
        public int NbrRoute { get; set; } = 0;
        /*
        public string ListeEtapes
        {
            get { return string.Join(", ", Etapes.Select(c => c.Name)); }
        }
        */
    }
    #endregion
}
