namespace UrlShortenerAPI.Models
{
    public class TopUrlDto
    {
        public int UrlId { get; set; }
        public string ShortCode { get; set; } = null!;
        public string ShortUrl { get; set; } = null!;
        public string LongUrl { get; set; } = null!;
        public int Clicks { get; set; }
        public DateTime? LastAccessedAt { get; set; }
    }
}
