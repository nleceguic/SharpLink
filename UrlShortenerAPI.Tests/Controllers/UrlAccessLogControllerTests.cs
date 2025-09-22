using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using UrlShortenerAPI.Controllers;
using UrlShortenerAPI.Data;
using UrlShortenerAPI.Models;

namespace UrlShortenerAPI.Tests.Controllers
{
    public class UrlAccessLogControllerTests
    {
        private ApiContext GetInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<ApiContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            var context = new ApiContext(options);

            context.Urls.Add(new Url { Id = 1, ShortCode = "abc123", LongUrl = "https://example.com" });
            context.UrlAccessLogs.AddRange(
                new UrlAccessLog { Id = 1, UrlId = 1, AccessedAt = DateTime.UtcNow.AddHours(-3), IpAddress = "127.0.0.1", UserAgent = "Agent1" },
                new UrlAccessLog { Id = 2, UrlId = 1, AccessedAt = DateTime.UtcNow.AddHours(-2), IpAddress = "127.0.0.2", UserAgent = "Agent2" },
                new UrlAccessLog { Id = 3, UrlId = 1, AccessedAt = DateTime.UtcNow.AddHours(-1), IpAddress = "127.0.0.3", UserAgent = "Agent3" }
            );

            context.SaveChanges();
            return context;
        }

        [Fact]
        public void GetAccessLogs_ReturnsNotFound_WhenUrlDoesNotExist()
        {
            var context = GetInMemoryContext();
            var controller = new UrlAccessLogController(context, NullLogger<UrlAccessLogController>.Instance);
            controller.ControllerContext.HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();

            var result = controller.GetAccessLogs(999);

            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal("URL no encontrada.", notFoundResult.Value);
        }

        [Fact]
        public void GetAccessLogs_ReturnsLogs_WhenUrlExists()
        {
            var context = GetInMemoryContext();
            var controller = new UrlAccessLogController(context, NullLogger<UrlAccessLogController>.Instance);
            controller.ControllerContext.HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();

            var result = controller.GetAccessLogs(1, pageNumber: 1, pageSize: 2);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var value = Assert.IsType<AccessLogResponseDto>(okResult.Value);

            Assert.Equal(1, value.UrlId);
            Assert.Equal("abc123", value.ShortCode);
            Assert.Equal(2, value.PageSize);
            Assert.Equal(3, value.TotalLogs);
            Assert.Equal(2, ((System.Collections.IEnumerable)value.Logs).Cast<dynamic>().Count());
            Assert.NotNull(value.FirstAccess);
            Assert.NotNull(value.LastAccess);
        }

        [Fact]
        public void GetAccessLogs_Pagination_WorksCorrectly()
        {
            var context = GetInMemoryContext();
            var controller = new UrlAccessLogController(context, NullLogger<UrlAccessLogController>.Instance);
            controller.ControllerContext.HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();

            var result = controller.GetAccessLogs(1, pageNumber: 2, pageSize: 1);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var value = Assert.IsType<AccessLogResponseDto>(okResult.Value);

            Assert.Equal(2, value.PageNumber);
            Assert.Equal(1, value.PageSize);
            Assert.Single(value.Logs);
            Assert.Equal(3, value.TotalLogs);
            Assert.Equal(3, value.TotalPages);
        }
    }
}
