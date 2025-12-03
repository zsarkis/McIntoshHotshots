using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using McIntoshHotshots.db;
using PuppeteerSharp;

namespace McIntoshHotshots.Tests.Integration;

/// <summary>
/// Base class for integration tests that configures WebApplicationFactory with in-memory database
/// and provides browser automation via PuppeteerSharp
/// This prevents tests from requiring a real PostgreSQL connection
/// TODO: Move to real PostgreSQL container in CI (see issue for Option 2)
/// </summary>
public class IntegrationTestBase
{
    protected static WebApplicationFactory<Program>? Factory { get; private set; }
    protected static IBrowser? Browser { get; private set; }
    protected static string? BaseUrl { get; private set; }

    protected static async Task InitializeTestInfrastructure()
    {
        // Set up WebApplicationFactory with in-memory database
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
                        options.UseInMemoryDatabase($"IntegrationTestDb_{Guid.NewGuid()}");
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

        var client = Factory.CreateClient();
        BaseUrl = client.BaseAddress?.ToString().TrimEnd('/');

        // Set up PuppeteerSharp for browser automation
        var browserFetcher = new BrowserFetcher();
        await browserFetcher.DownloadAsync();
        Browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
        });
    }

    protected static async Task CleanupTestInfrastructure()
    {
        if (Browser != null)
        {
            await Browser.CloseAsync();
        }
        Factory?.Dispose();
    }
}
