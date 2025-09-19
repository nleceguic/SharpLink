namespace UrlShortenerAPI.Models
{
    public class UrlAccessLog
    {
        public int Id { get; set; }
        public int UrlId { get; set; }
        public DateTime AccessedAt { get; set; } = DateTime.UtcNow;
        public string IpAddress { get; set; } = null!;
        public string UserAgent { get; set; } = null!;

        public Url url { get; set; }
    }
}
