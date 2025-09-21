using Azure.Core;
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
        private readonly ILogger<UrlController> _logger;

        public UrlController(ApiContext context, ILogger<UrlController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // POST: api/url/shorten
        [HttpPost("shorten")]
        public IActionResult Shorten([FromBody] UrlCreateDto request)
        {
            _logger.LogInformation("Petición recibida para acortar la URL: {LongUrl}", request.LongUrl);

            if (string.IsNullOrEmpty(request.LongUrl))
            {
                _logger.LogWarning("La URL proporcionada está vacía.");
                return BadRequest("La URL no puede estar vacía.");
            }

            if (!Uri.TryCreate(request.LongUrl, UriKind.Absolute, out Uri validatedUri)
                || (validatedUri.Scheme != Uri.UriSchemeHttp && validatedUri.Scheme != Uri.UriSchemeHttps))
            {
                _logger.LogWarning("La URL proporcionada no es válida: {LongUrl}", request.LongUrl);
                return BadRequest("La URL no es válida. Debe comenzar con http:// o https://");
            }

            string sanitizedLongUrl = InputSanitizer.SanitizeUrl(validatedUri.ToString());

            string shortCode;

            if (!string.IsNullOrEmpty(request.CustomAlias))
            {
                request.CustomAlias = InputSanitizer.SanitizeAlias(request.CustomAlias);

                if (string.IsNullOrWhiteSpace(request.CustomAlias))
                {
                    _logger.LogWarning("El alias personalizado contiene caracteres inválidos: {Alias}", request.CustomAlias);
                    return BadRequest("El alias contiene caracteres inválidos.");
                }


                bool exists = _context.Urls.Any(u => u.ShortCode == request.CustomAlias);
                if (exists)
                {
                    _logger.LogWarning("El alias {Alias} ya está en uso.", request.CustomAlias);
                    return Conflict("El alias ya está en uso. Elige otro.");
                }

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

            _logger.LogInformation("URL corta creada: {ShortCode}, original: {LongUrl}", shortCode, sanitizedLongUrl);

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
            _logger.LogInformation("Petición de redirección para el shortcode: {ShortCode}", shortCode);

            var url = _context.Urls.FirstOrDefault(u => u.ShortCode == shortCode);
            if (url == null)
            {
                _logger.LogWarning("No se encontró ninguna URL con el shortcode: {ShortCode}", shortCode);
                return NotFound("Short URL no encontrada.");
            }

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
                _logger.LogWarning("El enlace {ShortCode} ha expirado.", shortCode);
                return BadRequest("El enlace ha expirado.");
            }

            if (!url.IsActive)
            {
                _logger.LogWarning("El enlace {ShortCode} está desactivado.", shortCode);
                return BadRequest("El enlace ha sido desactivado.");
            }

            _logger.LogInformation(
                "Redirigiendo shortcode {ShortCode} hacia {LongUrl}. Total de clics: {Clicks}, Último acceso: {LastAccessedAt}, IP: {Ip}, UserAgent: {UserAgent}",
                shortCode,
                url.LongUrl,
                url.Clicks,
                url.LastAccessedAt,
                accessLog.IpAddress,
                accessLog.UserAgent
            );

            return Redirect(url.LongUrl);
        }

        // GET: api/url/{id}
        [HttpGet("urls/{id}")]
        public IActionResult GetById(int id)
        {
            _logger.LogInformation("Petición recibida para obtener la URL con Id: {Id}", id);

            var url = _context.Urls.Find(id);

            if (url == null)
            {
                _logger.LogWarning("No se encontró ninguna URL con el Id: {Id}", id);
                return NotFound("URL no encontrada.");
            }

            var shortUrl = $"{Request.Scheme}://{Request.Host}/{url.ShortCode}";

            _logger.LogInformation("URL encontrada: Id {Id}, ShortCode {ShortCode}, Clics {Clicks}, Activa: {IsActive}",
                url.Id, url.ShortCode, url.Clicks, url.IsActive);

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
            _logger.LogInformation("Petición para listar URLs. Página: {PageNumber}, Tamaño: {PageSize}", pageNumber, pageSize);

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

            _logger.LogInformation("Se devolvieron {Count} URLs de un total de {TotalUrls} (Página {PageNumber}/{TotalPages})",
                urls.Count, totalUrls, pageNumber, result.totalPages);

            return Ok(result);
        }

        // PUT: api/url/urls/{id}
        [HttpPut("urls/{id}")]
        public IActionResult UpdateUrl(int id, [FromBody] UrlUpdateDto request)
        {
            _logger.LogInformation("Petición recibida para actualizar la URL con Id: {Id}", id);

            var url = _context.Urls.Find(id);

            if (url == null)
            {
                _logger.LogWarning("No se encontró ninguna URL con el Id: {Id}", id);
                return NotFound("URL no encontrada.");
            }

            if (string.IsNullOrEmpty(request.LongUrl))
            {
                _logger.LogWarning("La nueva URL proporcionada está vacía para Id: {Id}", id);
                return BadRequest("La URL no puede estar vacia.");
            }

            var oldLongUrl = url.LongUrl;
            url.LongUrl = request.LongUrl;

            _context.SaveChanges();

            var shortUrl = $"{Request.Scheme}://{Request.Host}/{url.ShortCode}";

            _logger.LogInformation("La URL con Id {Id} se actualizó. Antes: {OldLongUrl}, Ahora: {NewLongUrl}",
                url.Id, oldLongUrl, url.LongUrl);

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
            _logger.LogInformation("Petición para cambiar estado de URL Id: {Id} a {IsActive}", id, isActive);

            var url = _context.Urls.Find(id);
            if (url == null)
            {
                _logger.LogWarning("No se encontró ninguna URL con el Id: {Id}", id);
                return NotFound("URL no encontrada.");
            }

            var oldStatus = url.IsActive;
            url.IsActive = isActive;
            _context.SaveChanges();

            _logger.LogInformation("El estado de la URL con Id {Id} cambió de {OldStatus} a {NewStatus}",
                url.Id, oldStatus, url.IsActive);

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
            _logger.LogInformation("Petición para eliminar URL con Id: {Id}", id);

            var url = _context.Urls.Find(id);

            if (url == null)
            {
                _logger.LogWarning("No se encontró ninguna URL con el Id: {Id}", id);
                return NotFound("URL no encontrada.");
            }

            _context.Urls.Remove(url);
            _context.SaveChanges();

            _logger.LogInformation("URL con Id {Id} eliminada correctamente.", id);

            return Ok(new { message = $"URL con ID {id} eliminada correctamente." });
        }

        // GET: api/url/expand/{shortCode}
        [HttpGet("expand/{shortCode}")]
        public IActionResult Expand(string shortCode)
        {
            _logger.LogInformation("Petición para expandir el shortcode: {ShortCode}", shortCode);

            var url = _context.Urls.FirstOrDefault(u => u.ShortCode == shortCode);
            if (url == null)
            {
                _logger.LogWarning("No se encontró ninguna URL con shortcode: {ShortCode}", shortCode);
                return NotFound(new { message = "No se ha encontrado una URL con esos datos." });
            }

            if (!url.IsActive)
            {
                _logger.LogWarning("El shortcode {ShortCode} está desactivado.", shortCode);
                return BadRequest(new { message = "Esta URL está desactivada." });
            }

            if (url.ExpiresAt.HasValue && url.ExpiresAt < DateTime.UtcNow)
            {
                _logger.LogWarning("El shortcode {ShortCode} ha expirado.", shortCode);
                return BadRequest(new { message = "Esta URL ha expirado." });
            }

            _logger.LogInformation("Shortcode {ShortCode} expandido a {LongUrl}", shortCode, url.LongUrl);

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