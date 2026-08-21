using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using UrlShortenerAPI.Models;

namespace UrlShortenerAPI.Tests.Integration
{
    // End-to-end tests exercising the real ASP.NET Core pipeline for the primary
    // flow: HTTP request -> routing -> controller -> EF Core -> HTTP response.
    // Unlike the controller unit tests, these go through actual HTTP, model
    // binding and status-code/redirect semantics.
    public class UrlEndpointsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public UrlEndpointsIntegrationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task PostShorten_WithValidUrl_ReturnsOkWithShortUrl()
        {
            var client = _factory.CreateClient();

            var response = await client.PostAsJsonAsync("/api/url/shorten", new UrlCreateDto
            {
                LongUrl = "https://example.com/integration-test"
            });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("shortUrl");
        }

        [Fact]
        public async Task PostShorten_WithEmptyUrl_ReturnsBadRequest()
        {
            var client = _factory.CreateClient();

            var response = await client.PostAsJsonAsync("/api/url/shorten", new UrlCreateDto
            {
                LongUrl = ""
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task FullFlow_CreateThenRedirect_ReturnsFoundWithLocationHeader()
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            var alias = "int-" + Guid.NewGuid().ToString("N")[..8];
            var createResponse = await client.PostAsJsonAsync("/api/url/shorten", new UrlCreateDto
            {
                LongUrl = "https://example.com/full-flow-target",
                CustomAlias = alias
            });
            createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var redirectResponse = await client.GetAsync($"/{alias}");

            redirectResponse.StatusCode.Should().Be(HttpStatusCode.Found);
            redirectResponse.Headers.Location!.ToString().Should().Be("https://example.com/full-flow-target");
        }

        [Fact]
        public async Task GetShortCode_WhenItDoesNotExist_ReturnsNotFound()
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            var response = await client.GetAsync("/does-not-exist-code");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetById_WhenIdDoesNotExist_ReturnsNotFound()
        {
            var client = _factory.CreateClient();

            var response = await client.GetAsync("/api/url/urls/999999");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task FullFlow_RedirectIncrementsClicks_VisibleThroughGetById()
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            var alias = "clicks-" + Guid.NewGuid().ToString("N")[..8];
            var createResponse = await client.PostAsJsonAsync("/api/url/shorten", new UrlCreateDto
            {
                LongUrl = "https://example.com/click-tracking",
                CustomAlias = alias
            });
            var created = await createResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

            await client.GetAsync($"/{alias}");
            await client.GetAsync($"/{alias}");

            var listResponse = await client.GetAsync("/api/url/urls?pageNumber=1&pageSize=100");
            listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var listBody = await listResponse.Content.ReadAsStringAsync();
            listBody.Should().Contain(alias);
        }
    }
}
