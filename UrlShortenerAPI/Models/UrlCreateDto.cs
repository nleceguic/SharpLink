namespace UrlShortenerAPI.Models
{
    public class UrlCreateDto
    {
        public string LongUrl { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
        public string? CustomAlias { get; set; }
    }
}
