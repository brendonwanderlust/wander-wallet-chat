using System.Text.Json.Serialization;

namespace wander_wallet_chat
{
    public class ChatRequest
    {
        [JsonPropertyName("userId")]
        public string? UserId { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("latitude")]
        public decimal Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public decimal Longitude { get; set; }

        [JsonPropertyName("measurementSystem")]
        public MeasurementSystem MeasurementSystem { get; set; }

        [JsonPropertyName("activities")]
        public string[] Activities { get; set; } = Array.Empty<string>();
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MeasurementSystem
    {
        [JsonPropertyName("imperial")]
        Imperial,

        [JsonPropertyName("metric")]
        Metric
    }
}