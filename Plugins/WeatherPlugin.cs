using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;

namespace wander_wallet_chat.Plugins
{
    public class WeatherPlugin
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WeatherPlugin> _logger;
        private readonly string _apiKey;
        private readonly string _baseUrl = "https://weather.visualcrossing.com/VisualCrossingWebServices/rest/services/timeline";

        public WeatherPlugin(HttpClient httpClient, ILogger<WeatherPlugin> logger)
        {
            _logger = logger;
            _httpClient = httpClient;
            _apiKey = Environment.GetEnvironmentVariable("WEATHER_API_KEY")
                ?? throw new InvalidOperationException("WEATHER_API_KEY environment variable is required");
        }

        [KernelFunction("get_weather")]
        [Description("Get current weather and forecast for a specific location. Use this when users ask about weather conditions in any city or location or if it might be relevant to their specific request")]
        public async Task<string> GetWeatherAsync(
            [Description("The location to get weather for (e.g., 'Paris, France', 'New York', 'Tokyo')")] string location,
            [Description("Temperature unit system: 'metric' for Celsius or 'us' for Fahrenheit. Default is 'us'.")] string unitGroup = "us")
        {
            try
            {
                // Validate and sanitize inputs
                if (string.IsNullOrWhiteSpace(location))
                {
                    return "I need a location to get weather information. Please specify a city or location.";
                }

                // Ensure unitGroup is valid
                unitGroup = unitGroup.ToLower() switch
                {
                    "metric" or "celsius" => "metric",
                    "us" or "imperial" or "fahrenheit" => "us",
                    _ => "us" // Default to US units
                };

                var encodedLocation = Uri.EscapeDataString(location);
                var url = $"{_baseUrl}/{encodedLocation}?unitGroup={unitGroup}&key={_apiKey}";

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return $"Sorry, I couldn't get weather information for {location}. The weather service might be unavailable or the location wasn't found.";
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var weatherData = JsonSerializer.Deserialize<WeatherResponse>(jsonContent);

                if (weatherData == null)
                {
                    return $"Sorry, I couldn't parse the weather data for {location}.";
                }

                return FormatWeatherResponse(weatherData, unitGroup, _logger);
            }
            catch (HttpRequestException ex)
            {
                return $"I'm having trouble connecting to the weather service right now. Please try again later.";
            }
            catch (TaskCanceledException ex)
            {
                return $"The weather request timed out. Please try again.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting weather for {Location}", location);
                return $"Sorry, I encountered an error getting weather information for {location}.";
            }
        }

        private static string FormatWeatherResponse(WeatherResponse weather, string unitGroup, ILogger<WeatherPlugin> _logger)
        {
            var tempUnit = unitGroup == "metric" ? "°C" : "°F";
            var response = new StringBuilder();
            try
            {
                response.AppendLine($"🌤️ **Weather for {weather.ResolvedAddress}**");
                response.AppendLine();

                // Current conditions
                if (weather.CurrentConditions != null)
                {
                    var current = weather.CurrentConditions;
                    response.AppendLine($"**Current Conditions:**");
                    response.AppendLine($"• Temperature: {current.Temp:F1}{tempUnit}");
                    response.AppendLine($"• Conditions: {current.Conditions}");

                    if (current.FeelsLike.HasValue)
                        response.AppendLine($"• Feels like: {current.FeelsLike:F1}{tempUnit}");

                    if (current.Humidity.HasValue)
                        response.AppendLine($"• Humidity: {current.Humidity:F0}%");

                    if (current.WindSpeed.HasValue)
                    {
                        var windUnit = unitGroup == "metric" ? "km/h" : "mph";
                        response.AppendLine($"• Wind: {current.WindSpeed:F1} {windUnit}");
                    }

                    response.AppendLine();
                }

                // Today's forecast
                if (weather.Days?.Count > 0)
                {
                    var today = weather.Days[0];
                    response.AppendLine($"**Today's Forecast:**");
                    response.AppendLine($"• High: {today.TempMax:F1}{tempUnit} / Low: {today.TempMin:F1}{tempUnit}");
                    response.AppendLine($"• Conditions: {today.Conditions}");

                    if (!string.IsNullOrEmpty(today.Description))
                        response.AppendLine($"• {today.Description}");

                    response.AppendLine();
                }

                // 3-day forecast
                if (weather.Days?.Count > 1)
                {
                    response.AppendLine($"**3-Day Forecast:**");
                    for (int i = 1; i < Math.Min(4, weather.Days.Count); i++)
                    {
                        var day = weather.Days[i];
                        var date = DateTime.Parse(day.DateTime).ToString("ddd, MMM d");
                        response.AppendLine($"• {date}: {day.TempMax:F1}/{day.TempMin:F1}{tempUnit} - {day.Conditions}");
                    }
                }

                // Weather alerts if any
                if (weather.Alerts?.Count > 0)
                {
                    response.AppendLine();
                    response.AppendLine($"⚠️ **Weather Alerts:**");
                    foreach (var alert in weather.Alerts.Take(2)) // Limit to 2 alerts
                    {
                        response.AppendLine($"• {alert.Event}: {alert.Description}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error formatting weather response", weather);
            }

            return response.ToString();
        }
    }

    public class WeatherResponse
    {
        [JsonPropertyName("queryCost")]
        public int QueryCost { get; set; }

        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }

        [JsonPropertyName("resolvedAddress")]
        public string ResolvedAddress { get; set; }

        [JsonPropertyName("address")]
        public string Address { get; set; }

        [JsonPropertyName("timezone")]
        public string Timezone { get; set; }

        [JsonPropertyName("tzoffset")]
        public double TimezoneOffset { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("days")]
        public List<DayWeather> Days { get; set; }

        [JsonPropertyName("alerts")]
        public List<WeatherAlert> Alerts { get; set; }

        [JsonPropertyName("currentConditions")]
        public CurrentConditions CurrentConditions { get; set; }
    }

    public class DayWeather
    {
        [JsonPropertyName("datetime")]
        public string DateTime { get; set; }

        [JsonPropertyName("datetimeEpoch")]
        public long DateTimeEpoch { get; set; }

        [JsonPropertyName("tempmax")]
        public double? TempMax { get; set; }

        [JsonPropertyName("tempmin")]
        public double? TempMin { get; set; }

        [JsonPropertyName("temp")]
        public double? Temp { get; set; }

        [JsonPropertyName("feelslike")]
        public double? FeelsLike { get; set; }

        [JsonPropertyName("humidity")]
        public double? Humidity { get; set; }

        [JsonPropertyName("precip")]
        public double? Precipitation { get; set; }

        [JsonPropertyName("precipprob")]
        public double? PrecipitationProbability { get; set; }

        [JsonPropertyName("windspeed")]
        public double? WindSpeed { get; set; }

        [JsonPropertyName("windgust")]
        public double? WindGust { get; set; }

        [JsonPropertyName("winddir")]
        public double? WindDirection { get; set; }

        [JsonPropertyName("pressure")]
        public double? Pressure { get; set; }

        [JsonPropertyName("cloudcover")]
        public double? CloudCover { get; set; }

        [JsonPropertyName("visibility")]
        public double? Visibility { get; set; }

        [JsonPropertyName("uvindex")]
        public double? UVIndex { get; set; }

        [JsonPropertyName("sunrise")]
        public string Sunrise { get; set; }

        [JsonPropertyName("sunset")]
        public string Sunset { get; set; }

        [JsonPropertyName("moonphase")]
        public double? MoonPhase { get; set; }

        [JsonPropertyName("conditions")]
        public string Conditions { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("icon")]
        public string Icon { get; set; } 

        [JsonPropertyName("source")]
        public string Source { get; set; }

        [JsonPropertyName("hours")]
        public List<HourWeather> Hours { get; set; }
    }

    public class HourWeather
    {
        [JsonPropertyName("datetime")]
        public string DateTime { get; set; }

        [JsonPropertyName("datetimeEpoch")]
        public long DateTimeEpoch { get; set; }

        [JsonPropertyName("temp")]
        public double? Temp { get; set; }

        [JsonPropertyName("feelslike")]
        public double? FeelsLike { get; set; }

        [JsonPropertyName("humidity")]
        public double? Humidity { get; set; }

        [JsonPropertyName("dew")]
        public double? DewPoint { get; set; }

        [JsonPropertyName("precip")]
        public double? Precipitation { get; set; }

        [JsonPropertyName("precipprob")]
        public double? PrecipitationProbability { get; set; }

        [JsonPropertyName("snow")]
        public double? Snow { get; set; }

        [JsonPropertyName("snowdepth")]
        public double? SnowDepth { get; set; }

        [JsonPropertyName("windspeed")]
        public double? WindSpeed { get; set; }

        [JsonPropertyName("windgust")]
        public double? WindGust { get; set; }

        [JsonPropertyName("winddir")]
        public double? WindDirection { get; set; }

        [JsonPropertyName("pressure")]
        public double? Pressure { get; set; }

        [JsonPropertyName("cloudcover")]
        public double? CloudCover { get; set; }

        [JsonPropertyName("visibility")]
        public double? Visibility { get; set; }

        [JsonPropertyName("uvindex")]
        public double? UVIndex { get; set; }

        [JsonPropertyName("conditions")]
        public string Conditions { get; set; }

        [JsonPropertyName("icon")]
        public string Icon { get; set; } 

        [JsonPropertyName("source")]
        public string Source { get; set; }
    }

    public class CurrentConditions
    {
        [JsonPropertyName("datetime")]
        public string DateTime { get; set; }

        [JsonPropertyName("datetimeEpoch")]
        public long DateTimeEpoch { get; set; }

        [JsonPropertyName("temp")]
        public double? Temp { get; set; }

        [JsonPropertyName("feelslike")]
        public double? FeelsLike { get; set; }

        [JsonPropertyName("humidity")]
        public double? Humidity { get; set; }

        [JsonPropertyName("dew")]
        public double? DewPoint { get; set; }

        [JsonPropertyName("precip")]
        public double? Precipitation { get; set; }

        [JsonPropertyName("precipprob")]
        public double? PrecipitationProbability { get; set; }

        [JsonPropertyName("snow")]
        public double? Snow { get; set; }

        [JsonPropertyName("snowdepth")]
        public double? SnowDepth { get; set; }

        [JsonPropertyName("windspeed")]
        public double? WindSpeed { get; set; }

        [JsonPropertyName("windgust")]
        public double? WindGust { get; set; }

        [JsonPropertyName("winddir")]
        public double? WindDirection { get; set; }

        [JsonPropertyName("pressure")]
        public double? Pressure { get; set; }

        [JsonPropertyName("cloudcover")]
        public double? CloudCover { get; set; }

        [JsonPropertyName("visibility")]
        public double? Visibility { get; set; }

        [JsonPropertyName("uvindex")]
        public double? UVIndex { get; set; }

        [JsonPropertyName("conditions")]
        public string Conditions { get; set; }

        [JsonPropertyName("icon")]
        public string Icon { get; set; } 

        [JsonPropertyName("source")]
        public string Source { get; set; }
    }

    public class WeatherAlert
    {
        [JsonPropertyName("event")]
        public string Event { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("starts")]
        public string Starts { get; set; }

        [JsonPropertyName("ends")]
        public string Ends { get; set; }

        [JsonPropertyName("severity")]
        public string Severity { get; set; }

        [JsonPropertyName("uri")]
        public string Uri { get; set; }
    }

    public class StationInfo
    {
        [JsonPropertyName("distance")]
        public double? Distance { get; set; }

        [JsonPropertyName("latitude")]
        public double? Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double? Longitude { get; set; }

        [JsonPropertyName("useCount")]
        public int? UseCount { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("quality")]
        public int? Quality { get; set; }

        [JsonPropertyName("contribution")]
        public int? Contribution { get; set; }
    }

}
