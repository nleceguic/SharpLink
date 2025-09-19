using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UrlShortenerAPI.Data;
using UrlShortenerAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace UrlShortenerAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AnalyticsController : ControllerBase
    {
        private readonly ApiContext _context;

        public AnalyticsController(ApiContext context)
        {
            _context = context;
        }

        // GET: api/analytics/top
        [HttpGet("top")]
        public IActionResult GetTopUrls()
        {
            var topUrls = _context.Urls
                .OrderByDescending(u => u.Clicks)
                .Take(10)
                .Select(u => new TopUrlDto
                {
                    UrlId = u.Id,
                    ShortCode = u.ShortCode,
                    ShortUrl = $"{Request.Scheme}://{Request.Host}/{u.ShortCode}",
                    Clicks = u.Clicks,
                    LastAccessedAt = u.LastAccessedAt
                })
                .ToList();

            return Ok(topUrls);
        }

        // GET: api/analytics/topByDate?fromDate=2025-09-10&toDate=2025-09-18
        [HttpGet("topByDate")]
        public IActionResult GetTopUrlsByDate([FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null)
        {
            var query = _context.UrlAccessLogs.AsQueryable();

            if (fromDate.HasValue)
                query = query.Where(l => l.AccessedAt >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(l => l.AccessedAt <= toDate.Value);

            var topUrls = query
                .GroupBy(l => l.UrlId)
                .Select(g => new
                {
                    UrlId = g.Key,
                    Clicks = g.Count()
                })
                .OrderByDescending(x => x.Clicks)
                .Take(10)
                .Join(_context.Urls,
                    log => log.UrlId,
                    url => url.Id,
                    (log, url) => new TopUrlDto
                    {
                        UrlId = url.Id,
                        ShortCode = url.ShortCode,
                        ShortUrl = $"{Request.Scheme}://{Request.Host}/{url.ShortCode}",
                        LongUrl = url.LongUrl,
                        Clicks = log.Clicks,
                        LastAccessedAt = url.LastAccessedAt
                    })
                .ToList();

            return Ok(topUrls);
        }
    }
}
