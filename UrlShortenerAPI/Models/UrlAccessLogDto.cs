namespace UrlShortenerAPI.Models
{
    public class UrlAccessLogDto
    {
        public int Id { get; set; }
        public DateTime AccessedAt { get; set; }
        public string IpAddress { get; set; } = null!;
        public string UserAgent { get; set; } = null!;
    }
}
