using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UrlShortenerAPI.Data;

namespace UrlShortenerAPI.Tests.Integration
{
    // Swaps the real SQL Server-backed ApiContext for an isolated InMemory database
    // per factory instance, so integration tests exercise the full HTTP pipeline
    // (routing, model binding, controllers, EF Core) without needing a real database.
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbName = Guid.NewGuid().ToString();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                var descriptorsToRemove = services
                    .Where(d =>
                        d.ServiceType == typeof(DbContextOptions<ApiContext>) ||
                        d.ServiceType == typeof(ApiContext) ||
                        (d.ServiceType.FullName?.Contains("DbContextOptions") ?? false))
                    .ToList();

                foreach (var descriptor in descriptorsToRemove)
                    services.Remove(descriptor);

                services.AddDbContext<ApiContext>(options =>
                    options.UseInMemoryDatabase(_dbName));
            });
        }
    }
}
