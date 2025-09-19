using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Net;
using System.Text.RegularExpressions;
using UrlShortenerAPI.Data;
using UrlShortenerAPI.Helpers;
using UrlShortenerAPI.Models;

namespace UrlShortenerAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UrlController : ControllerBase
    {
        private readonly ApiContext _context;

        public UrlController(ApiContext context)
        {
            _context = context;
        }

        // POST: api/url/shorten
        [HttpPost("shorten")]
        public IActionResult Shorten([FromBody] UrlCreateDto request)
        {
            if (string.IsNullOrEmpty(request.LongUrl))
                return BadRequest("La URL no puede estar vacía.");

            if (!Uri.TryCreate(request.LongUrl, UriKind.Absolute, out Uri validatedUri)
                || (validatedUri.Scheme != Uri.UriSchemeHttp && validatedUri.Scheme != Uri.UriSchemeHttps))
            {
                return BadRequest("La URL no es válida. Debe comenzar con http:// o https://");
            }

            string sanitizedLongUrl = InputSanitizer.SanitizeUrl(validatedUri.ToString());

            string shortCode;

            if (!string.IsNullOrEmpty(request.CustomAlias))
            {
                request.CustomAlias = InputSanitizer.SanitizeAlias(request.CustomAlias);

                if (string.IsNullOrWhiteSpace(request.CustomAlias))
                    return BadRequest("El alias contiene caracteres inválidos.");

                bool exists = _context.Urls.Any(u => u.ShortCode == request.CustomAlias);
                if (exists)
                    return Conflict("El alias ya está en uso. Elige otro.");

                shortCode = request.CustomAlias;
            }
            else
            {
                shortCode = Guid.NewGuid().ToString("N")[..6];
            }

            var shortUrl = $"{Request.Scheme}://{Request.Host}/{shortCode}";

            string qrFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "qrcodes");
            string qrFileName = shortCode;
            string qrPath = QrCodeHelper.GenerateQrCode(shortUrl, qrFolder, qrFileName);

            var url = new Url
            {
                LongUrl = sanitizedLongUrl,
                ShortCode = shortCode,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = request.ExpiresAt,
                IsActive = true,
                Clicks = 0,
                QrCodePath = $"/qrcodes/{qrFileName}.png"
            };

            _context.Urls.Add(url);
            _context.SaveChanges();

            return Ok(new
            {
                originalUrl = WebUtility.HtmlEncode(url.LongUrl),
                shortUrl = WebUtility.HtmlEncode(shortUrl),
                createdAt = url.CreatedAt,
                expiresAt = url.ExpiresAt,
                isActive = url.IsActive,
                qrCodePath = url.QrCodePath
            });
        }


        // GET /{shortCode}
        [HttpGet("/{shortCode}")]
        public IActionResult RedirectToLongUrl(string shortCode)
        {
            var url = _context.Urls.FirstOrDefault(u => u.ShortCode == shortCode);
            if (url == null)
                return NotFound("Short URL no encontrada.");

            url.Clicks++;
            url.LastAccessedAt = DateTime.UtcNow;

            var accessLog = new UrlAccessLog
            {
                UrlId = url.Id,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers["User-Agent"].ToString()
            };

            _context.UrlAccessLogs.Add(accessLog);

            _context.SaveChanges();

            if (url.ExpiresAt.HasValue && url.ExpiresAt < DateTime.UtcNow)
            {
                return BadRequest("El enlace ha expirado.");
            }

            if (!url.IsActive)
            {
                return BadRequest("El enlace ha sido desactivado.");
            }

            return Redirect(url.LongUrl);
        }

        // GET: api/url/{id}
        [HttpGet("urls/{id}")]
        public IActionResult GetById(int id)
        {
            var url = _context.Urls.Find(id);

            if (url == null)
                return NotFound("URL no encontrada.");

            var shortUrl = $"{Request.Scheme}://{Request.Host}/{url.ShortCode}";

            return Ok(new
            {
                id = url.Id,
                longUrl = url.LongUrl,
                shortCode = url.ShortCode,
                shortUrl,
                clicks = url.Clicks,
                createdAt = url.CreatedAt,
                lastAccessedAt = url.LastAccessedAt,
                expiresAt = url.ExpiresAt,
                isActive = true,
            });
        }

        // GET: api/url/urls?pageNumber=1&pageSize=10
        [HttpGet("urls")]
        public IActionResult GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;

            var totalUrls = _context.Urls.Count();

            var urls = _context.Urls
                .OrderByDescending(u => u.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new
                {
                    id = u.Id,
                    longUrl = u.LongUrl,
                    shortCode = u.ShortCode,
                    shortUrl = $"{Request.Scheme}://{Request.Host}/{u.ShortCode}",
                    clicks = u.Clicks,
                    createdAt = u.CreatedAt,
                    lastAccessedAt = u.LastAccessedAt,
                    expiresAt = u.ExpiresAt,
                    isActive = true
                })
                .ToList();

            var result = new
            {
                pageNumber,
                pageSize,
                totalUrls,
                totalPages = (int)Math.Ceiling((double)totalUrls / pageSize),
                urls
            };

            return Ok(result);
        }

        // PUT: api/url/urls/{id}
        [HttpPut("urls/{id}")]
        public IActionResult UpdateUrl(int id, [FromBody] UrlUpdateDto request)
        {
            var url = _context.Urls.Find(id);

            if (url == null)
                return NotFound("URL no encontrada.");

            if (string.IsNullOrEmpty(request.LongUrl))
                return BadRequest("La URL no puede estar vacia.");

            url.LongUrl = request.LongUrl;

            _context.SaveChanges();

            var shortUrl = $"{Request.Scheme}://{Request.Host}/{url.ShortCode}";

            return Ok(new
            {
                id = url.Id,
                longUrl = url.LongUrl,
                shortCode = url.ShortCode,
                shortUrl,
                clicks = url.Clicks,
                createdAt = url.CreatedAt,
                lastAccessedAt = url.LastAccessedAt,
                expiresAt = url.ExpiresAt
            });
        }

        // PUT: api/url/urls/{id}/status
        [HttpPut("urls/{id}/status")]
        public IActionResult SetActiveStatus(int id, [FromBody] bool isActive)
        {
            var url = _context.Urls.Find(id);
            if (url == null)
                return NotFound("URL no encontrada.");

            url.IsActive = isActive;
            _context.SaveChanges();

            return Ok(new
            {
                id = url.Id,
                shortCode = url.ShortCode,
                isActive = url.IsActive
            });
        }

        // DELETE: api/url/urls/{id}
        [HttpDelete("urls/{id}")]
        public IActionResult DeleteUrl(int id)
        {
            var url = _context.Urls.Find(id);

            if (url == null)
                return NotFound("URL no encontrada.");

            _context.Urls.Remove(url);
            _context.SaveChanges();

            return Ok(new { message = $"URL con ID {id} eliminada correctamente." });
        }

        [HttpGet("expand/{shortCode}")]
        public IActionResult Expand(string shortCode)
        {
            var url = _context.Urls.FirstOrDefault(u => u.ShortCode == shortCode);

            if (url == null)
                return NotFound(new { message = "No se ha encontrado una URL con esos datos." });

            if (!url.IsActive)
                return BadRequest(new { message = "Esta URL esta desactivada." });

            if (url.ExpiresAt.HasValue && url.ExpiresAt < DateTime.UtcNow)
                return BadRequest(new { message = "Esta URL ha expirado." });

            return Ok(new
            {
                longUrl = url.LongUrl,
                shortCode = url.ShortCode,
                shortUrl = $"{Request.Scheme}://{Request.Host}/{url.ShortCode}",
                createdAt = url.CreatedAt,
                expiresAt = url.ExpiresAt,
                lastAccessedAt = url.LastAccessedAt,
                clicks = url.Clicks,
                isActive = url.IsActive,
                qrCodePath = url.QrCodePath,
                status = url.ExpiresAt.HasValue && url.ExpiresAt < DateTime.UtcNow
            ? "expirado"
            : (url.IsActive ? "activo" : "inactivo")
            });
        }
    }
}
