using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UrlShortenerAPI.Models;
using UrlShortenerAPI.Data;
using Microsoft.EntityFrameworkCore.Metadata;

namespace UrlShortenerAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UrlAccessLogController : ControllerBase
    {
        private readonly ApiContext _context;

        public UrlAccessLogController(ApiContext context)
        {
            _context = context;
        }

        // GET: api/url/urls/{id}/accesslogs?pageNumber=1&pageSize=10
        [HttpGet("urls/{id}/accesslogs")]
        public IActionResult GetAccessLogs(int id, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var url = _context.Urls.Find(id);
            if (url == null)
                return NotFound("URL no encontrada.");


            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;

            var totalLogs = _context.UrlAccessLogs.Count(l => l.UrlId == id);

            var logs = _context.UrlAccessLogs
                .Where(l => l.UrlId == id)
                .OrderByDescending(l => l.AccessedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new UrlAccessLogDto
                {
                    Id = l.Id,
                    AccessedAt = l.AccessedAt,
                    IpAddress = l.IpAddress,
                    UserAgent = l.UserAgent
                })
                .ToList();

            var totalClicks = logs.Count > 0 ? logs.Count : 0;
            var firstAccess = _context.UrlAccessLogs.Where(l => l.UrlId == id).OrderBy(l => l.AccessedAt).Select(l => l.AccessedAt).FirstOrDefault();
            var lastAccess = logs.FirstOrDefault()?.AccessedAt;

            var result = new
            {
                urlId = url.Id,
                shortCode = url.ShortCode,
                shortUrl = $"{Request.Scheme}://{Request.Host}/{url.ShortCode}",
                pageNumber,
                pageSize,
                totalLogs,
                totalPages = (int)Math.Ceiling((double)totalLogs / pageSize),
                firstAccess,
                lastAccess,
                logs
            };

            return Ok(result);
        }
    }
}
