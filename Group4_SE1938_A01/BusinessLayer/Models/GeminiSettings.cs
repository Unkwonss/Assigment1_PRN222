namespace BusinessLayer.Models
{
    public class GeminiSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "gemini-2.5-flash";
        public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/models";
        public int MaxOutputTokens { get; set; } = 1024;
        public double Temperature { get; set; } = 0.3;
    }
}

