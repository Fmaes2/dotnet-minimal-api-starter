using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StarterKit.Api.Data;

namespace StarterKit.Tests
{
    public class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("DataSource=:memory:");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Jwt:Key", "test-key-with-at-least-32-characters!!");

            builder.ConfigureServices(services =>
            {
                // Remove everything EF registered for AppDbContext (including the Npgsql configuration)
                var toRemove = services
                    .Where(d => d.ServiceType == typeof(AppDbContext)
                             || d.ServiceType.GenericTypeArguments.Contains(typeof(AppDbContext)))
                    .ToList();
                foreach (var d in toRemove) services.Remove(d);

                _connection.Open();
                services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
            });
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            var host = base.CreateHost(builder);
            using var scope = host.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
            return host;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) _connection.Dispose();
        }
    }
}
