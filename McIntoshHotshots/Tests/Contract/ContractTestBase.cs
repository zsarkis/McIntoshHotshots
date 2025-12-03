using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using McIntoshHotshots.db;
using System.Net.Http;

namespace McIntoshHotshots.Tests.Contract;

/// <summary>
/// Base class for contract tests that configures WebApplicationFactory with in-memory database
/// This prevents tests from requiring a real PostgreSQL connection
/// </summary>
public class ContractTestBase
{
    protected static WebApplicationFactory<Program>? Factory { get; private set; }
    protected static HttpClient? Client { get; private set; }

    protected static void InitializeTestFactory()
    {
        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove the real DbContext registration
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }

                    // Add in-memory database instead
                    services.AddDbContext<ApplicationDbContext>(options =>
                    {
                        options.UseInMemoryDatabase("TestDatabase");
                    });

                    // Build the service provider and create the database
                    var sp = services.BuildServiceProvider();
                    using (var scope = sp.CreateScope())
                    {
                        var scopedServices = scope.ServiceProvider;
                        var db = scopedServices.GetRequiredService<ApplicationDbContext>();

                        // Ensure the database is created
                        db.Database.EnsureCreated();
                    }
                });
            });

        Client = Factory.CreateClient();
    }

    protected static void CleanupTestFactory()
    {
        Client?.Dispose();
        Factory?.Dispose();
    }
}
