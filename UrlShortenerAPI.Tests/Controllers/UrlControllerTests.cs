using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UrlShortenerAPI.Controllers;
using UrlShortenerAPI.Data;
using UrlShortenerAPI.Models;
using UrlShortenerAPI.Helpers;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;

namespace UrlShortenerAPI.Tests.Controllers
{
    public class UrlControllerTests
    {
        private readonly ILogger<UrlController> _logger;

        public UrlControllerTests()
        {
            var mockLogger = new Mock<ILogger<UrlController>>();
            _logger = mockLogger.Object;
        }

        // -----------------------------
        // Helper: DbContext InMemory
        // -----------------------------
        private ApiContext GetInMemoryContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApiContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            return new ApiContext(options);
        }

        // -----------------------------
        // Helper: Simular HttpContext
        // -----------------------------
        private void SetupHttpContext(UrlController controller)
        {
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            controller.ControllerContext.HttpContext.Request.Scheme = "https";
            controller.ControllerContext.HttpContext.Request.Host = new HostString("localhost:7238");
            controller.ControllerContext.HttpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
            controller.ControllerContext.HttpContext.Request.Headers["User-Agent"] = "UnitTestAgent";
        }

        // -----------------------------
        // POST /shorten
        // -----------------------------
        [Fact]
        public void Shorten_ShouldReturnOk_WhenValidUrl()
        {
            using var context = GetInMemoryContext("ShortenTestDb");
            var controller = new UrlController(context, _logger);
            SetupHttpContext(controller);

            var request = new UrlCreateDto { LongUrl = "https://ejemplo.com" };
            var result = controller.Shorten(request) as OkObjectResult;

            result.Should().NotBeNull();
            result.StatusCode.Should().Be(200);
            context.Urls.Count().Should().Be(1);
        }

        [Fact]
        public void Shorten_ShouldReturnBadRequest_WhenUrlIsEmpty()
        {
            using var context = GetInMemoryContext("ShortenEmptyTestDb");
            var controller = new UrlController(context, _logger);
            SetupHttpContext(controller);

            var request = new UrlCreateDto { LongUrl = "" };
            var result = controller.Shorten(request) as BadRequestObjectResult;

            result.Should().NotBeNull();
            result.StatusCode.Should().Be(400);
            result.Value.Should().Be("La URL no puede estar vacía.");
        }

        [Fact]
        public void Shorten_ShouldReturnBadRequest_WhenUrlIsInvalid()
        {
            using var context = GetInMemoryContext("ShortenInvalidTestDb");
            var controller = new UrlController(context, _logger);
            SetupHttpContext(controller);

            var request = new UrlCreateDto { LongUrl = "notaurl" };
            var result = controller.Shorten(request) as BadRequestObjectResult;

            result.Should().NotBeNull();
            result.StatusCode.Should().Be(400);
            result.Value.Should().Be("La URL no es válida. Debe comenzar con http:// o https://");
        }

        [Fact]
        public void Shorten_ShouldReturnBadRequest_WhenSchemeIsNotHttpOrHttps()
        {
            using var context = GetInMemoryContext("ShortenFtpSchemeTestDb");
            var controller = new UrlController(context, _logger);
            SetupHttpContext(controller);

            var request = new UrlCreateDto { LongUrl = "ftp://ejemplo.com/file.txt" };
            var result = controller.Shorten(request) as BadRequestObjectResult;

            result.Should().NotBeNull();
            result.StatusCode.Should().Be(400);
            context.Urls.Count().Should().Be(0);
        }

        [Fact]
        public void Shorten_ShouldReturnBadRequest_WhenUrlIsWhitespace()
        {
            using var context = GetInMemoryContext("ShortenWhitespaceTestDb");
            var controller = new UrlController(context, _logger);
            SetupHttpContext(controller);

            var request = new UrlCreateDto { LongUrl = "   " };
            var result = controller.Shorten(request) as BadRequestObjectResult;

            result.Should().NotBeNull();
            result.StatusCode.Should().Be(400);
            context.Urls.Count().Should().Be(0);
        }

        [Fact]
        public void Shorten_ShouldUseCustomAlias_WhenProvidedAndAvailable()
        {
            using var context = GetInMemoryContext("ShortenCustomAliasTestDb");
            var controller = new UrlController(context, _logger);
            SetupHttpContext(controller);

            var request = new UrlCreateDto { LongUrl = "https://ejemplo.com", CustomAlias = "mi-alias" };
            var result = controller.Shorten(request) as OkObjectResult;

            result.Should().NotBeNull();
            result.StatusCode.Should().Be(200);
            context.Urls.First().ShortCode.Should().Be("mi-alias");
        }

        [Fact]
        public void Shorten_ShouldReturnBadRequest_WhenCustomAliasHasOnlyInvalidChars()
        {
            using var context = GetInMemoryContext("ShortenAliasInvalidCharsTestDb");
            var controller = new UrlController(context, _logger);
            SetupHttpContext(controller);

            var request = new UrlCreateDto { LongUrl = "https://ejemplo.com", CustomAlias = "@@@" };
            var result = controller.Shorten(request) as BadRequestObjectResult;

            result.Should().NotBeNull();
            result.StatusCode.Should().Be(400);
            result.Value.Should().Be("El alias contiene caracteres inválidos.");
            context.Urls.Count().Should().Be(0);
        }

        [Fact]
        public void Shorten_ShouldReturnConflict_WhenCustomAliasAlreadyInUse()
        {
            using var context = GetInMemoryContext("ShortenAliasConflictTestDb");
            context.Urls.Add(new Url { LongUrl = "https://existing.com", ShortCode = "mi-alias", CreatedAt = DateTime.UtcNow, IsActive = true });
            context.SaveChanges();

            var controller = new UrlController(context, _logger);
            SetupHttpContext(controller);

            var request = new UrlCreateDto { LongUrl = "https://ejemplo.com", CustomAlias = "mi-alias" };
            var result = controller.Shorten(request) as ConflictObjectResult;

            result.Should().NotBeNull();
            result.StatusCode.Should().Be(409);
            context.Urls.Count().Should().Be(1);
        }

        [Fact]
        public void Shorten_ShouldGenerateDifferentShortCodes_ForDifferentRequests()
        {
            using var context = GetInMemoryContext("ShortenUniqueCodesTestDb");
            var controller = new UrlController(context, _logger);
            SetupHttpContext(controller);

            controller.Shorten(new UrlCreateDto { LongUrl = "https://one.com" });
            controller.Shorten(new UrlCreateDto { LongUrl = "https://two.com" });

            var codes = context.Urls.Select(u => u.ShortCode).ToList();
            codes.Should().HaveCount(2);
            codes.Distinct().Should().HaveCount(2);
        }

        // -----------------------------
        // GET /{shortCode} Redirect
        // -----------------------------
        [Fact]
        public void RedirectToLongUrl_ShouldRedirect_WhenUrlExists()
        {
            using var context = GetInMemoryContext("RedirectTestDb");

            var url = new Url
            {
                LongUrl = "https://ejemplo.com",
                ShortCode = "abc123",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            context.Urls.Add(url);
            context.SaveChanges();

            var controller = new UrlController(context, _logger);
            SetupHttpContext(controller);

            var result = controller.RedirectToLongUrl("abc123") as RedirectResult;

            result.Should().NotBeNull();
            result.Url.Should().Be("https://ejemplo.com");
        }

        [Fact]
        public void RedirectToLongUrl_ShouldReturnNotFound_WhenUrlDoesNotExist()
        {
            using var context = GetInMemoryContext("RedirectNotFoundDb");
            var controller = new UrlController(context, _logger);
            SetupHttpContext(controller);

            var result = controller.RedirectToLongUrl("nonexistent") as NotFoundObjectResult;

            result.Should().NotBeNull();
            result.StatusCode.Should().Be(404);
            result.Value.Should().Be("Short URL no encontrada.");
        }

        [Fact]
        public void RedirectToLongUrl_ShouldIncrementClicksAndLogAccess_WhenUrlExists()
        {
            using var context = GetInMemoryContext("RedirectIncrementsClicksDb");

            var url = new Url { LongUrl = "https://ejemplo.com", ShortCode = "abc123", CreatedAt = DateTime.UtcNow, IsActive = true, Clicks = 0 };
            context.Urls.Add(url);
            context.SaveChanges();

            var controller = new UrlController(context, _logger);
            SetupHttpContext(controller);

            controller.RedirectToLongUrl("abc123");

            var updated = context.Urls.First();
            updated.Clicks.Should().Be(1);
            updated.LastAccessedAt.Should().NotBeNull();
            context.UrlAccessLogs.Count(l => l.UrlId == url.Id).Should().Be(1);
        }

        [Fact]
        public void RedirectToLongUrl_ShouldReturnBadRequest_WhenLinkHasExpired()
        {
            using var context = GetInMemoryContext("RedirectExpiredDb");

            var url = new Url
            {
                LongUrl = "https://ejemplo.com",
                ShortCode = "abc123",
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                ExpiresAt = DateTime.UtcNow.AddDays(-1)
            };
            context.Urls.Add(url);
            context.SaveChanges();

            var controller = new UrlController(context, _logger);
            SetupHttpContext(controller);

            var result = controller.RedirectToLongUrl("abc123") as BadRequestObjectResult;

            result.Should().NotBeNull();
            result.StatusCode.Should().Be(400);
            result.Value.Should().Be("El enlace ha expirado.");

            // Characterization test: the controller currently records the click/access log
            // for the expired link *before* returning the error. This may be unintended
            // (it pollutes analytics with clicks on dead links) - see project findings.
            context.Urls.First().Clicks.Should().Be(1);
        }

        [Fact]
        public void RedirectToLongUrl_ShouldRedirect_WhenExpiresAtIsInTheFuture()
        {
            using var context = GetInMemoryContext("RedirectFutureExpiryDb");

            var url = new Url
            {
                LongUrl = "https://ejemplo.com",
                ShortCode = "abc123",
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                ExpiresAt = DateTime.UtcNow.AddDays(1)
            };
            context.Urls.Add(url);
            context.SaveChanges();

            var controller = new UrlController(context, _logger);
            SetupHttpContext(controller);

            var result = controller.RedirectToLongUrl("abc123") as RedirectResult;

            result.Should().NotBeNull();
            result.Url.Should().Be("https://ejemplo.com");
        }

        [Fact]
        public void RedirectToLongUrl_ShouldReturnBadRequest_WhenLinkIsInactive()
        {
            using var context = GetInMemoryContext("RedirectInactiveDb");

            var url = new Url
            {
                LongUrl = "https://ejemplo.com",
                ShortCode = "abc123",
                CreatedAt = DateTime.UtcNow,
                IsActive = false
            };
            context.Urls.Add(url);
            context.SaveChanges();

            var controller = new UrlController(context, _logger);
            SetupHttpContext(controller);

            var result = controller.RedirectToLongUrl("abc123") as BadRequestObjectResult;

            result.Should().NotBeNull();
            result.StatusCode.Should().Be(400);
            result.Value.Should().Be("El enlace ha sido desactivado.");
        }

        // -----------------------------
        // GET api/url/urls/{id}
        // -----------------------------
        [Fact]
        public void GetById_ShouldReturnOk_WhenUrlExists()
        {
            using var context = GetInMemoryContext("GetByIdTestDb");

            var url = new Url { LongUrl = "https://ejemplo.com", ShortCode = "abc123", CreatedAt = DateTime.UtcNow, IsActive = true };
            context.Urls.Add(url);
            context.SaveChanges();

            var controller = new UrlController(context, _logger);
            SetupHttpContext(controller);

            var result = controller.GetById(url.Id) as OkObjectResult;

            result.Should().NotBeNull();
            result.StatusCode.Should().Be(200);
        }

        [Fact]
        public void GetById_ShouldReturnNotFound_WhenUrlDoesNotExist()
        {
            using var context = GetInMemoryContext("GetByIdNotFoundDb");
            var controller = new UrlController(context, _logger);
            SetupHttpContext(controller);

            var result = controller.GetById(999) as NotFoundObjectResult;

            result.Should().NotBeNull();
            result.StatusCode.Should().Be(404);
        }

        [Fact]
        public void GetById_ShouldReflectActualIsActiveState_WhenUrlIsInactive()
        {
            using var context = GetInMemoryContext("GetByIdInactiveDb");

            var url = new Url { LongUrl = "https://ejemplo.com", ShortCode = "abc123", CreatedAt = DateTime.UtcNow, IsActive = false };
            context.Urls.Add(url);
            context.SaveChanges();

            var controller = new UrlController(context, _logger);
            SetupHttpContext(controller);

            var result = controller.GetById(url.Id) as OkObjectResult;

            result.Should().NotBeNull();
            var isActive = (bool)result!.Value!.GetType().GetProperty("isActive")!.GetValue(result.Value)!;
            isActive.Should().BeFalse();
        }

        // -----------------------------
        // GET api/url/urls (paginated list)
        // -----------------------------
        [Fact]
        public void GetAll_ShouldReturnPagedResults_OrderedByCreatedAtDescending()
        {
            using var context = GetInMemoryContext("GetAllPagedDb");

            context.Urls.AddRange(
                new Url { ShortCode = "old", LongUrl = "https://old.com", CreatedAt = DateTime.UtcNow.AddDays(-2), IsActive = true },
                new Url { ShortCode = "mid", LongUrl = "https://mid.com", CreatedAt = DateTime.UtcNow.AddDays(-1), IsActive = true },
                new Url { ShortCode = "new", LongUrl = "https://new.com", CreatedAt = DateTime.UtcNow, IsActive = true }
            );
            context.SaveChanges();

            var controller = new UrlController(context, _logger);
            SetupHttpContext(controller);

            var result = controller.GetAll(pageNumber: 1, pageSize: 2) as OkObjectResult;

            result.Should().NotBeNull();
            var totalUrls = (int)result!.Value!.GetType().GetProperty("totalUrls")!.GetValue(result.Value)!;
            var urls = (System.Collections.IEnumerable)result.Value!.GetType().GetProperty("urls")!.GetValue(result.Value)!;
            var shortCodes = urls.Cast<object>()
                .Select(u => (string)u.GetType().GetProperty("shortCode")!.GetValue(u)!)
                .ToList();

            totalUrls.Should().Be(3);
            shortCodes.Should().Equal("new", "mid");
        }

        [Fact]
        public void GetAll_ShouldReflectActualIsActiveState()
        {
            using var context = GetInMemoryContext("GetAllIsActiveDb");

            context.Urls.Add(new Url { ShortCode = "off", LongUrl = "https://off.com", CreatedAt = DateTime.UtcNow, IsActive = false });
            context.SaveChanges();

            var controller = new UrlController(context, _logger);
            SetupHttpContext(controller);

            var result = controller.GetAll() as OkObjectResult;

            var urls = (System.Collections.IEnumerable)result!.Value!.GetType().GetProperty("urls")!.GetValue(result.Value)!;
            var first = urls.Cast<object>().First();
            var isActive = (bool)first.GetType().GetProperty("isActive")!.GetValue(first)!;

            isActive.Should().BeFalse();
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(-1, -5)]
        public void GetAll_ShouldNormalizeInvalidPagingParams(int pageNumber, int pageSize)
        {
            using var context = GetInMemoryContext($"GetAllNormalizeDb_{pageNumber}_{pageSize}");
            context.Urls.Add(new Url { ShortCode = "a", LongUrl = "https://a.com", CreatedAt = DateTime.UtcNow, IsActive = true });
            context.SaveChanges();

            var controller = new UrlController(context, _logger);
            SetupHttpContext(controller);

            var result = controller.GetAll(pageNumber, pageSize) as OkObjectResult;

            result.Should().NotBeNull();
            var actualPageNumber = (int)result!.Value!.GetType().GetProperty("pageNumber")!.GetValue(result.Value)!;
            var actualPageSize = (int)result.Value!.GetType().GetProperty("pageSize")!.GetValue(result.Value)!;

            actualPageNumber.Should().Be(1);
            actualPageSize.Should().Be(10);
        }

        // -----------------------------
        // PUT api/url/urls/{id}
        // -----------------------------
        [Fact]
        public void UpdateUrl_ShouldReturnOk_WhenUrlExists()
        {
            using var context = GetInMemoryContext("UpdateUrlTestDb");

            var url = new Url { LongUrl = "https://old.com", ShortCode = "abc123", CreatedAt = DateTime.UtcNow, IsActive = true };
            context.Urls.Add(url);
            context.SaveChanges();

            var controller = new UrlController(context, _logger);
            SetupHttpContext(controller);

            var request = new UrlUpdateDto { LongUrl = "https://new.com" };
            var result = controller.UpdateUrl(url.Id, request) as OkObjectResult;

            result.Should().NotBeNull();
            result.StatusCode.Should().Be(200);
            context.Urls.First().LongUrl.Should().Be("https://new.com");
        }

        [Fact]
        public void UpdateUrl_ShouldReturnNotFound_WhenUrlDoesNotExist()
        {
            using var context = GetInMemoryContext("UpdateUrlNotFoundDb");
            var controller = new UrlController(context, _logger);
            SetupHttpContext(controller);

            var request = new UrlUpdateDto { LongUrl = "https://new.com" };
            var result = controller.UpdateUrl(999, request) as NotFoundObjectResult;

            result.Should().NotBeNull();
            result.StatusCode.Should().Be(404);
        }

        [Fact]
        public void UpdateUrl_ShouldReturnBadRequest_WhenLongUrlIsEmpty()
        {
            using var context = GetInMemoryContext("UpdateUrlEmptyDb");

            var url = new Url { LongUrl = "https://old.com", ShortCode = "abc123", CreatedAt = DateTime.UtcNow, IsActive = true };
            context.Urls.Add(url);
            context.SaveChanges();

            var controller = new UrlController(context, _logger);
            SetupHttpContext(controller);

            var request = new UrlUpdateDto { LongUrl = "" };
            var result = controller.UpdateUrl(url.Id, request) as BadRequestObjectResult;

            result.Should().NotBeNull();
            result.StatusCode.Should().Be(400);
            context.Urls.First().LongUrl.Should().Be("https://old.com");
        }

        // -----------------------------
        // PUT api/url/urls/{id}/status
        // -----------------------------
        [Fact]
        public void SetActiveStatus_ShouldReturnOk_WhenUrlExists()
        {
            using var context = GetInMemoryContext("SetStatusTestDb");

            var url = new Url { LongUrl = "https://ejemplo.com", ShortCode = "abc123", CreatedAt = DateTime.UtcNow, IsActive = false };
            context.Urls.Add(url);
            context.SaveChanges();

            var controller = new UrlController(context, _logger);
            SetupHttpContext(controller);

            var result = controller.SetActiveStatus(url.Id, true) as OkObjectResult;

            result.Should().NotBeNull();
            result.StatusCode.Should().Be(200);
            context.Urls.First().IsActive.Should().BeTrue();
        }

        [Fact]
        public void SetActiveStatus_ShouldReturnNotFound_WhenUrlDoesNotExist()
        {
            using var context = GetInMemoryContext("SetStatusNotFoundDb");
            var controller = new UrlController(context, _logger);
            SetupHttpContext(controller);

            var result = controller.SetActiveStatus(999, true) as NotFoundObjectResult;

            result.Should().NotBeNull();
            result.StatusCode.Should().Be(404);
        }

        // -----------------------------
        // DELETE api/url/urls/{id}
        // -----------------------------
        [Fact]
        public void DeleteUrl_ShouldReturnOk_WhenUrlExists()
        {
            using var context = GetInMemoryContext("DeleteUrlTestDb");

            var url = new Url { LongUrl = "https://ejemplo.com", ShortCode = "abc123", CreatedAt = DateTime.UtcNow };
            context.Urls.Add(url);
            context.SaveChanges();

            var controller = new UrlController(context, _logger);
            SetupHttpContext(controller);

            var result = controller.DeleteUrl(url.Id) as OkObjectResult;

            result.Should().NotBeNull();
            result.StatusCode.Should().Be(200);
            context.Urls.Count().Should().Be(0);
        }

        [Fact]
        public void DeleteUrl_ShouldReturnNotFound_WhenUrlDoesNotExist()
        {
            using var context = GetInMemoryContext("DeleteUrlNotFoundDb");
            var controller = new UrlController(context, _logger);
            SetupHttpContext(controller);

            var result = controller.DeleteUrl(999) as NotFoundObjectResult;

            result.Should().NotBeNull();
            result.StatusCode.Should().Be(404);
        }

        // -----------------------------
        // GET api/url/expand/{shortCode}
        // -----------------------------
        [Fact]
        public void Expand_ShouldReturnOk_WhenUrlExistsAndActive()
        {
            using var context = GetInMemoryContext("ExpandTestDb");

            var url = new Url { LongUrl = "https://ejemplo.com", ShortCode = "abc123", CreatedAt = DateTime.UtcNow, IsActive = true };
            context.Urls.Add(url);
            context.SaveChanges();

            var controller = new UrlController(context, _logger);
            SetupHttpContext(controller);

            var result = controller.Expand("abc123") as OkObjectResult;

            result.Should().NotBeNull();
            result.StatusCode.Should().Be(200);
        }

        [Fact]
        public void Expand_ShouldReturnNotFound_WhenUrlDoesNotExist()
        {
            using var context = GetInMemoryContext("ExpandNotFoundDb");
            var controller = new UrlController(context, _logger);
            SetupHttpContext(controller);

            var result = controller.Expand("nonexistent") as NotFoundObjectResult;

            result.Should().NotBeNull();
            result.StatusCode.Should().Be(404);
        }

        [Fact]
        public void Expand_ShouldReturnBadRequest_WhenUrlIsInactive()
        {
            using var context = GetInMemoryContext("ExpandInactiveDb");

            var url = new Url { LongUrl = "https://ejemplo.com", ShortCode = "abc123", CreatedAt = DateTime.UtcNow, IsActive = false };
            context.Urls.Add(url);
            context.SaveChanges();

            var controller = new UrlController(context, _logger);
            SetupHttpContext(controller);

            var result = controller.Expand("abc123") as BadRequestObjectResult;

            result.Should().NotBeNull();
            result.StatusCode.Should().Be(400);
        }

        [Fact]
        public void Expand_ShouldReturnBadRequest_WhenUrlHasExpired()
        {
            using var context = GetInMemoryContext("ExpandExpiredDb");

            var url = new Url
            {
                LongUrl = "https://ejemplo.com",
                ShortCode = "abc123",
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                ExpiresAt = DateTime.UtcNow.AddDays(-1)
            };
            context.Urls.Add(url);
            context.SaveChanges();

            var controller = new UrlController(context, _logger);
            SetupHttpContext(controller);

            var result = controller.Expand("abc123") as BadRequestObjectResult;

            result.Should().NotBeNull();
            result.StatusCode.Should().Be(400);
        }

        [Fact]
        public void Expand_ShouldReturnOk_WhenExpiresAtIsInTheFuture()
        {
            using var context = GetInMemoryContext("ExpandFutureExpiryDb");

            var url = new Url
            {
                LongUrl = "https://ejemplo.com",
                ShortCode = "abc123",
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                ExpiresAt = DateTime.UtcNow.AddDays(1)
            };
            context.Urls.Add(url);
            context.SaveChanges();

            var controller = new UrlController(context, _logger);
            SetupHttpContext(controller);

            var result = controller.Expand("abc123") as OkObjectResult;

            result.Should().NotBeNull();
            result.StatusCode.Should().Be(200);
        }

        [Fact]
        public void Expand_ShouldReportStatusActivo_WhenUrlIsActiveAndNotExpired()
        {
            using var context = GetInMemoryContext("ExpandStatusActivoDb");

            var url = new Url { LongUrl = "https://ejemplo.com", ShortCode = "abc123", CreatedAt = DateTime.UtcNow, IsActive = true };
            context.Urls.Add(url);
            context.SaveChanges();

            var controller = new UrlController(context, _logger);
            SetupHttpContext(controller);

            var result = controller.Expand("abc123") as OkObjectResult;

            result.Should().NotBeNull();
            var status = (string)result!.Value!.GetType().GetProperty("status")!.GetValue(result.Value)!;
            status.Should().Be("activo");
        }
    }
}