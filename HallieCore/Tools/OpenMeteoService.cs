using ExternalServices;
using System.Text.Json;

namespace Hallie.Tools
{
    #region Interface IWeatherService
    public interface IWeatherService
    {
        Task<WeatherData?> GetWeatherAsync(string location);
        Task<WeatherForecast?> GetForecastAsync(string location, int days = 3);
    }
    #endregion

    #region WeatherApiComService qui implémente IWeatherService
    public class WeatherApiComService : IWeatherService
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;

        public WeatherApiComService(string url, string apiKey)
        {
            _http = new HttpClient
            {
                BaseAddress = new Uri($"{url}")
            };
            _apiKey = apiKey;
        }

        public async Task<WeatherData?> GetWeatherAsync(string location)
        {
            try
            {
                var url = $"weather?q={location}&units=metric&appid={_apiKey}&lang=fr";
                var json = await _http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                var weather = doc.RootElement.GetProperty("weather")[0].GetProperty("description").GetString();
                var temp = doc.RootElement.GetProperty("main").GetProperty("temp").GetDouble();
                if (weather == null) 
                    weather = "Météo introuvale";

                return new WeatherData
                {
                    Location = location,
                    Temperature = Math.Round(temp, 1),
                    FeelsLike = Math.Round(doc.RootElement.GetProperty("main").GetProperty("feels_like").GetDouble(),1),
                    Humidity = doc.RootElement.GetProperty("main").GetProperty("humidity").GetInt32(),
                    WindSpeed = Math.Round(doc.RootElement.GetProperty("wind").GetProperty("speed").GetDouble() * 3.6,1), // Convertir de m/s en km/h
                    //Precipitation = current.GetProperty("precip_mm").GetDouble(),
                    Description = weather,
                    Timestamp = DateTime.UtcNow,
                    WeatherId = doc.RootElement.GetProperty("weather")[0].GetProperty("id").GetInt32()
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WeatherAPI] Erreur: {ex.Message}");
                return null;
            }
        }

        public async Task<WeatherForecast?> GetForecastAsync(string location, int days = 3)
        {
            try
            {
                var response = await _http.GetAsync($"forecast.json?key={_apiKey}&q={Uri.EscapeDataString(location)}&days={days}&lang=fr");
                if (!response.IsSuccessStatusCode)
                    return null;

                var content = await response.Content.ReadAsStringAsync();
                var json = JsonDocument.Parse(content);
                var forecastData = json.RootElement.GetProperty("forecast").GetProperty("forecastday");
                var locationData = json.RootElement.GetProperty("location");

                var forecast = new WeatherForecast
                {
                    Location = locationData.GetProperty("name").GetString()!,
                    Days = new List<DailyWeather>()
                };

                foreach (var day in forecastData.EnumerateArray())
                {
                    var dayData = day.GetProperty("day");
                    forecast.Days.Add(new DailyWeather
                    {
                        Date = DateTime.Parse(day.GetProperty("date").GetString()!),
                        MaxTemp = dayData.GetProperty("maxtemp_c").GetDouble(),
                        MinTemp = dayData.GetProperty("mintemp_c").GetDouble(),
                        //.Precipitation = dayData.GetProperty("totalprecip_mm").GetDouble(),
                        Description = dayData.GetProperty("condition").GetProperty("text").GetString()!
                    });
                }

                return forecast;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WeatherAPI] Erreur prévisions: {ex.Message}");
                return null;
            }
        }
    }
    #endregion

    #region WeatherTool 
    public class WeatherTool : ITool
    {
        public string Name => "get_weather";
        public string Description => "Obtient la météo actuelle et les prévisions pour une ville donnée";

        private readonly IWeatherService _weatherService;

        public WeatherTool(string webUrl, string webApiKey)
        {
            LoggerService.LogInfo("WeatherTool");
            var weatherService = new WeatherApiComService(webUrl, webApiKey);
            _weatherService = weatherService;
        }

        public WeatherTool(IWeatherService weatherService)
        {
            LoggerService.LogInfo("WeatherTool");

            _weatherService = weatherService;
        }

        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            LoggerService.LogInfo("WeatherTool.ExecuteAsync");

            try
            {
                var location = parameters["location"].ToString() ?? "";
                var includeForecast = parameters.ContainsKey("include_forecast")
                    && Convert.ToBoolean(parameters["include_forecast"]);

                if (string.IsNullOrWhiteSpace(location))
                {
                    var reponse = "Erreur : La ville ne peut pas être vide";
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        reponse = "",
                        error = reponse
                    });

                }

                // Obtenir la météo actuelle
                var current = await _weatherService.GetWeatherAsync(location);
                if (current == null)
                {
                    var reponse = $"Impossible d'obtenir la météo pour '{location}'. Vérifiez le nom de la ville.";
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        reponse = "",
                        error = reponse
                    });
                }

                var result = FormatCurrentWeather(current);

                // Ajouter les prévisions si demandé
                if (includeForecast)
                {
                    var forecast = await _weatherService.GetForecastAsync(location, 3);
                    if (forecast != null)
                    {
                        result += "\n\n" + FormatForecast(forecast);
                    }
                }
                return JsonService.Serialize(new
                {
                    ok = true,
                    reponse = result,
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
                    Name = "location",
                    Type = "string",
                    Description = "Le nom de la ville (ex: Paris, Lyon, Toulouse)",
                    Required = true
                },
                new ToolParameter
                {
                    Name = "include_forecast",
                    Type = "boolean",
                    Description = "Inclure les prévisions sur 3 jours (défaut: false)",
                    Required = false
                }
            };
        }

        private string FormatCurrentWeather(WeatherData data)
        {
            return $@"Météo actuelle à {data.Location} :
            - Température : {data.Temperature:F1}°C (ressenti {data.FeelsLike:F1}°C)
            - Conditions : {data.Description}
            - Humidité : {data.Humidity}%
            - Vent : {data.WindSpeed:F1} km/h";
            //- Précipitations : {data.Precipitation:F1} mm";
        }

        private string FormatForecast(WeatherForecast forecast)
        {
            var result = $"Prévisions pour {forecast.Location} :\n";

            foreach (var day in forecast.Days)
            {
                var dayName = day.Date.Date == DateTime.Today ? "Aujourd'hui" :
                              day.Date.Date == DateTime.Today.AddDays(1) ? "Demain" :
                              day.Date.ToString("dddd", System.Globalization.CultureInfo.GetCultureInfo("fr-FR"));

                result += $"\n{dayName} ({day.Date:dd/MM}) :\n";
                result += $"  - Températures : {day.MinTemp:F1}°C à {day.MaxTemp:F1}°C\n";
                result += $"  - Conditions : {day.Description}\n";
                //result += $"  - Précipitations : {day.Precipitation:F1} mm\n";
            }

            return result;
        }
    }
    #endregion

    #region Modèles de données météo
    public class WeatherData
    {
        public string Location { get; set; } = "";
        public double Temperature { get; set; }
        public double FeelsLike { get; set; }
        public int Humidity { get; set; }
        public double WindSpeed { get; set; }
        //public double Precipitation { get; set; }
        public string Description { get; set; } = "";
        public int WeatherId { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class WeatherForecast
    {
        public string Location { get; set; } = "";
        public List<DailyWeather> Days { get; set; } = new();
    }

    public class DailyWeather
    {
        public DateTime Date { get; set; }
        public double MaxTemp { get; set; }
        public double MinTemp { get; set; }
        //public double Precipitation { get; set; }
        public string Description { get; set; } = "";
    }
    #endregion
}
