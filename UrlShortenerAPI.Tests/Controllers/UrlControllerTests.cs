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
    }
}