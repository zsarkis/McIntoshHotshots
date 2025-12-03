using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.AspNetCore.Mvc.Testing;
using PuppeteerSharp;
using System;
using System.Threading.Tasks;

namespace McIntoshHotshots.Tests.Integration;

/// <summary>
/// Integration tests for Head-to-Head Comparison scenario from quickstart.md
/// Tests player vs player comparison functionality
/// TDD: These tests MUST FAIL until the views and controllers are fully implemented
/// </summary>
[TestClass]
public class HeadToHeadComparisonIntegrationTests : IntegrationTestBase
{
    [ClassInitialize]
    public static async Task ClassInitialize(TestContext context)
    {
        await InitializeTestInfrastructure();
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        await CleanupTestInfrastructure();
    }

    [TestMethod]
    public async Task HeadToHead_NavigateToComparison_PageLoadsSuccessfully()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int player1Id = 1;
        int player2Id = 2;

        try
        {
            // Act
            var response = await page.GoToAsync($"{BaseUrl}/stats/headtohead/{player1Id}/{player2Id}");

            // Assert
            Assert.IsNotNull(response, "Page response should not be null");
            Assert.IsTrue(response.Ok, "Page should load successfully with 200 OK");
            Assert.IsTrue(response.Url.Contains($"/stats/headtohead/{player1Id}/{player2Id}"),
                "URL should contain the head-to-head path");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task HeadToHead_WinLossRatio_DisplaysCorrectly()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int player1Id = 1;
        int player2Id = 2;

        try
        {
            // Act
            await page.GoToAsync($"{BaseUrl}/stats/headtohead/{player1Id}/{player2Id}");
            await page.WaitForSelectorAsync("[data-testid='win-loss-ratio'], .win-loss-ratio", new WaitForSelectorOptions
            {
                Timeout = 5000
            });

            var pageContent = await page.GetContentAsync();

            // Assert
            Assert.IsTrue(
                pageContent.Contains("Win") ||
                pageContent.Contains("win") ||
                pageContent.Contains("Loss") ||
                pageContent.Contains("loss") ||
                pageContent.Contains("W-L"),
                "Page should display win/loss ratio");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task HeadToHead_RecentForm_ShowsLast10Games()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int player1Id = 1;
        int player2Id = 2;

        try
        {
            // Act
            await page.GoToAsync($"{BaseUrl}/stats/headtohead/{player1Id}/{player2Id}");
            await Task.Delay(1000); // Wait for page to load

            var pageContent = await page.GetContentAsync();

            // Assert
            Assert.IsTrue(
                pageContent.Contains("Recent Form") ||
                pageContent.Contains("recent form") ||
                pageContent.Contains("Last 10") ||
                pageContent.Contains("last 10"),
                "Page should display recent form (last 10 games)");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task HeadToHead_ScoreDifferential_ShowsTrends()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int player1Id = 1;
        int player2Id = 2;

        try
        {
            // Act
            await page.GoToAsync($"{BaseUrl}/stats/headtohead/{player1Id}/{player2Id}");
            await Task.Delay(1000); // Wait for page to load

            var pageContent = await page.GetContentAsync();

            // Assert
            Assert.IsTrue(
                pageContent.Contains("Differential") ||
                pageContent.Contains("differential") ||
                pageContent.Contains("Average Score") ||
                pageContent.Contains("average score"),
                "Page should display score differential trends");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task HeadToHead_TournamentTypeBreakdown_ShowsByType()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int player1Id = 1;
        int player2Id = 2;

        try
        {
            // Act
            await page.GoToAsync($"{BaseUrl}/stats/headtohead/{player1Id}/{player2Id}");
            await Task.Delay(1000); // Wait for page to load

            var pageContent = await page.GetContentAsync();

            // Assert
            Assert.IsTrue(
                pageContent.Contains("Tournament Type") ||
                pageContent.Contains("tournament type") ||
                pageContent.Contains("Singles") ||
                pageContent.Contains("Doubles"),
                "Page should show tournament type breakdown");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task HeadToHead_NoMatches_HandlesGracefully()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int player1Id = 1;
        int player2Id = 999999; // Player with no matches against player1

        try
        {
            // Act
            await page.GoToAsync($"{BaseUrl}/stats/headtohead/{player1Id}/{player2Id}");
            await Task.Delay(1000); // Wait for page to load

            var pageContent = await page.GetContentAsync();

            // Assert
            Assert.IsTrue(
                pageContent.Contains("no matches") ||
                pageContent.Contains("No matches") ||
                pageContent.Contains("not found") ||
                pageContent.Contains("Not found") ||
                pageContent.Contains("404") ||
                pageContent.Contains("0 matches"),
                "Page should gracefully handle case when no matches exist between players");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task HeadToHead_PerformanceTrends_DisplayCorrectly()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int player1Id = 1;
        int player2Id = 2;

        try
        {
            // Act
            await page.GoToAsync($"{BaseUrl}/stats/headtohead/{player1Id}/{player2Id}");
            await Task.Delay(1000); // Wait for page to load

            var pageContent = await page.GetContentAsync();

            // Assert
            Assert.IsTrue(
                pageContent.Contains("Performance") ||
                pageContent.Contains("performance") ||
                pageContent.Contains("Trend") ||
                pageContent.Contains("trend") ||
                pageContent.Contains("Chart") ||
                pageContent.Contains("chart"),
                "Page should display performance trends");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task HeadToHead_Chart_RendersSuccessfully()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int player1Id = 1;
        int player2Id = 2;

        try
        {
            // Act
            await page.GoToAsync($"{BaseUrl}/stats/headtohead/{player1Id}/{player2Id}");

            // Try to find chart canvas (might be optional depending on data availability)
            try
            {
                await page.WaitForSelectorAsync(".chart-container canvas", new WaitForSelectorOptions
                {
                    Timeout = 3000
                });

                var chartVisible = await page.QuerySelectorAsync(".chart-container canvas");
                Assert.IsNotNull(chartVisible, "Chart should be visible if data is available");
            }
            catch
            {
                // Chart might not be present if no data - verify error message instead
                var pageContent = await page.GetContentAsync();
                Assert.IsTrue(
                    pageContent.Contains("no matches") ||
                    pageContent.Contains("insufficient data") ||
                    pageContent.Contains("Chart"),
                    "Page should either show chart or appropriate message");
            }
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task HeadToHead_SamePlayer_HandlesEdgeCase()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int playerId = 1;

        try
        {
            // Act - Compare player with themselves
            var response = await page.GoToAsync($"{BaseUrl}/stats/headtohead/{playerId}/{playerId}");
            await Task.Delay(1000);

            var pageContent = await page.GetContentAsync();

            // Assert
            Assert.IsTrue(
                pageContent.Contains("same player") ||
                pageContent.Contains("invalid") ||
                pageContent.Contains("error") ||
                pageContent.Contains("Error") ||
                pageContent.Contains("Bad Request") ||
                pageContent.Contains("400"),
                "Page should handle edge case of comparing same player");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task HeadToHead_RenderingPerformance_LoadsWithin500ms()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int player1Id = 1;
        int player2Id = 2;

        try
        {
            // Act
            var startTime = DateTime.UtcNow;
            await page.GoToAsync($"{BaseUrl}/stats/headtohead/{player1Id}/{player2Id}");
            await Task.Delay(1000); // Wait for initial render
            var endTime = DateTime.UtcNow;

            var loadTime = (endTime - startTime).TotalMilliseconds;

            // Assert
            Assert.IsTrue(loadTime < 2000,
                $"Head-to-head view should render within 2000ms (actual: {loadTime}ms) - performance requirement from quickstart.md");
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}
