using UrlShortenerAPI.Models;

public class AccessLogResponseDto
{
    public int UrlId { get; set; }
    public string ShortCode { get; set; }
    public string ShortUrl { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalLogs { get; set; }
    public int TotalPages { get; set; }
    public DateTime? FirstAccess { get; set; }
    public DateTime? LastAccess { get; set; }
    public List<UrlAccessLogDto> Logs { get; set; } = new();
}