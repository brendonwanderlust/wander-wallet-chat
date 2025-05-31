using System.ComponentModel;
using System.Text;
using System.Text.Json;
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
                _logger.LogInformation("Weather function called for: {Location}", location);

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
                    _logger.LogError( $"Weather call failed for {location}", $"Status code: {response.StatusCode}", $"Failure Reason: {response.ReasonPhrase}");
                    return $"Sorry, I couldn't get weather information for {location}. The weather service might be unavailable or the location wasn't found.";
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var weatherData = JsonSerializer.Deserialize<WeatherApiResponse>(jsonContent);

                if (weatherData == null)
                {
                    _logger.LogError($"Couldn't parse the weather data {location}", $"Content: {jsonContent}");
                    return $"Sorry, I couldn't parse the weather data for {location}.";
                }

                return FormatWeatherResponse(weatherData, unitGroup, _logger);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error getting weather for {Location}", location);
                return $"I'm having trouble connecting to the weather service right now. Please try again later.";
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Error getting weather for {Location}", location);
                return $"The weather request timed out. Please try again.";
            }
            catch (Exception ex)
            { 
                _logger.LogError(ex, "Error getting weather for {Location}", location);
                return $"Sorry, I encountered an error getting weather information for {location}.";
            }
        }

        private static string FormatWeatherResponse(WeatherApiResponse weather, string unitGroup, ILogger<WeatherPlugin> _logger)
        {
            _logger.LogError("Entered FormatWeatherResponse method");

            var tempUnit = unitGroup == "metric" ? "°C" : "°F";
            var response = new StringBuilder();

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
            if (weather.Days?.Length > 0)
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
            if (weather.Days?.Length > 1)
            {
                response.AppendLine($"**3-Day Forecast:**");
                for (int i = 1; i < Math.Min(4, weather.Days.Length); i++)
                {
                    var day = weather.Days[i];
                    var date = DateTime.Parse(day.DateTime).ToString("ddd, MMM d");
                    response.AppendLine($"• {date}: {day.TempMax:F1}/{day.TempMin:F1}{tempUnit} - {day.Conditions}");
                }
            }

            // Weather alerts if any
            if (weather.Alerts?.Length > 0)
            {
                response.AppendLine();
                response.AppendLine($"⚠️ **Weather Alerts:**");
                foreach (var alert in weather.Alerts.Take(2)) // Limit to 2 alerts
                {
                    response.AppendLine($"• {alert.Event}: {alert.Description}");
                }
            }

            return response.ToString();
        }
    }

    // Data models for the Weather API response
    public class WeatherApiResponse
    {
        public string? ResolvedAddress { get; set; }
        public string? Description { get; set; }
        public CurrentConditions? CurrentConditions { get; set; }
        public Day[]? Days { get; set; }
        public Alert[]? Alerts { get; set; }
    }

    public class CurrentConditions
    {
        public double Temp { get; set; }
        public double? FeelsLike { get; set; }
        public double? Humidity { get; set; }
        public double? WindSpeed { get; set; }
        public string? Conditions { get; set; }
        public string? Icon { get; set; }
    }

    public class Day
    {
        public string? DateTime { get; set; }
        public double TempMax { get; set; }
        public double TempMin { get; set; }
        public double Temp { get; set; }
        public string? Conditions { get; set; }
        public string? Description { get; set; }
        public string? Icon { get; set; }
    }

    public class Alert
    {
        public string? Event { get; set; }
        public string? Description { get; set; }
    }
}
