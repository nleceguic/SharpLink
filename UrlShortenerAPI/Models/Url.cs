namespace UrlShortenerAPI.Models
{
    public class Url
    {
        public int Id { get; set; }
        public String ShortCode { get; set; }
        public String LongUrl { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? ExpiresAt { get; set; }
        public DateTime? LastAccessedAt { get; set; }
        public int Clicks { get; set; }
        public string? QrCodePath { get; set; }
    }
}
