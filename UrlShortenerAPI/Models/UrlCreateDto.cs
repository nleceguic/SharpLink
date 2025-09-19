namespace UrlShortenerAPI.Models
{
    public class UrlCreateDto
    {
        public string LongUrl { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string? CustomAlias { get; set; }
    }
}
