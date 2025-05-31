namespace wander_wallet_chat
{
    public class ChatRequest
    {
        public string? UserId { get; set; }
        public string Message { get; set; } = string.Empty;
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public MeasurementSystem MeasurementSystem { get; set; }
        public string[] Activities { get; set; } = Array.Empty<string>();
    }

    public enum MeasurementSystem
    {
        Imperial,
        Metric
    }
}