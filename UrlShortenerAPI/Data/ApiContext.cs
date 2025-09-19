using Microsoft.EntityFrameworkCore;
using UrlShortenerAPI.Models;

namespace UrlShortenerAPI.Data
{
    public class ApiContext : DbContext
    {
        public DbSet<Url> Urls { get; set; }
        public DbSet<UrlAccessLog> UrlAccessLogs { get; set; }
        public ApiContext(DbContextOptions<ApiContext> options)
            : base(options)
        {

        }
    }
}
