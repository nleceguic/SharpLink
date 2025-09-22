using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Microsoft.Extensions.Logging;
using UrlShortenerAPI.Controllers;
using UrlShortenerAPI.Data;
using UrlShortenerAPI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Collections.Generic;

namespace UrlShortenerAPI.Tests.Controllers
{
    public class AnalyticsControllerTests
    {
        private readonly ILogger<AnalyticsController> _logger;

        public AnalyticsControllerTests()
        {
            var mockLogger = new Mock<ILogger<AnalyticsController>>();
            _logger = mockLogger.Object;
        }


        private ApiContext GetInMemoryContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApiContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            return new ApiContext(options);
        }

        private AnalyticsController GetController(ApiContext context)
        {
            var controller = new AnalyticsController(context, _logger);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Request =
                    {
                        Scheme = "https",
                        Host = new HostString("localhost:7238")
                    }
                }
            };
            return controller;
        }
        [Fact]
        public void GetTopUrls_ShouldReturnTop10Urls()
        {
            using var context = GetInMemoryContext("TopUrlsDb");

            for (int i = 0; i < 12; i++)
            {
                context.Urls.Add(new Url
                {
                    Id = i + 1,
                    ShortCode = $"code{i + 1}",
                    LongUrl = $"https://url{i + 1}.com",
                    Clicks = i,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                });
            }
            context.SaveChanges();

            var controller = GetController(context);
            var result = controller.GetTopUrls() as OkObjectResult;

            result.Should().NotBeNull();
            var list = result.Value as List<TopUrlDto>;
            list.Should().NotBeNull();
            list.Count.Should().Be(10);
            list.First().Clicks.Should().Be(11);
        }

        [Fact]
        public void GetTopUrlsByDate_ShouldFilterUrlsByAccessedAt()
        {
            using var context = GetInMemoryContext("TopUrlsByDateDb");

            var urls = new List<Url>
            {
                new Url { Id = 1, ShortCode = "a1", LongUrl = "https://a.com", CreatedAt = DateTime.UtcNow },
                new Url { Id = 2, ShortCode = "b2", LongUrl = "https://b.com", CreatedAt = DateTime.UtcNow }
            };
            context.Urls.AddRange(urls);

            context.UrlAccessLogs.AddRange(new[]
            {
                new UrlAccessLog { UrlId = 1, AccessedAt = DateTime.UtcNow.AddDays(-2), IpAddress="1.1.1.1", UserAgent="UA1" },
                new UrlAccessLog { UrlId = 1, AccessedAt = DateTime.UtcNow.AddDays(-1), IpAddress="1.1.1.1", UserAgent="UA1" },
                new UrlAccessLog { UrlId = 2, AccessedAt = DateTime.UtcNow.AddDays(-10), IpAddress="2.2.2.2", UserAgent="UA2" }
            });

            context.SaveChanges();

            var controller = GetController(context);
            var fromDate = DateTime.UtcNow.AddDays(-3);
            var toDate = DateTime.UtcNow;
            var result = controller.GetTopUrlsByDate(fromDate, toDate) as OkObjectResult;

            result.Should().NotBeNull();
            var list = result.Value as List<TopUrlDto>;
            list.Should().NotBeNull();
            list.Count.Should().Be(1);
            list.First().UrlId.Should().Be(1);
            list.First().Clicks.Should().Be(2);
        }

        [Fact]
        public void GetTopUrlsByDate_ShouldReturnEmptyList_WhenNoLogsInRange()
        {
            using var context = GetInMemoryContext("TopUrlsByDateEmptyDb");

            context.Urls.Add(new Url { Id = 1, ShortCode = "a1", LongUrl = "https://a.com", CreatedAt = DateTime.UtcNow });
            context.UrlAccessLogs.Add(new UrlAccessLog
            {
                UrlId = 1,
                AccessedAt = DateTime.UtcNow.AddDays(-10),
                IpAddress = "1.1.1.1",
                UserAgent = "UA1"
            });

            context.SaveChanges();

            var controller = GetController(context);
            var fromDate = DateTime.UtcNow.AddDays(-3);
            var toDate = DateTime.UtcNow;
            var result = controller.GetTopUrlsByDate(fromDate, toDate) as OkObjectResult;

            result.Should().NotBeNull();
            var list = result.Value as List<TopUrlDto>;
            list.Should().NotBeNull();
            list.Count.Should().Be(0);
        }
    }
}
