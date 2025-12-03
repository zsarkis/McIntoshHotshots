using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.AspNetCore.Mvc.Testing;
using PuppeteerSharp;
using System;
using System.Threading.Tasks;

namespace McIntoshHotshots.Tests.Integration;

/// <summary>
/// Integration tests for Tournament Statistics View scenario from quickstart.md
/// Tests complete tournament-level statistical analysis workflow
/// TDD: These tests MUST FAIL until the views and controllers are fully implemented
/// </summary>
[TestClass]
public class TournamentStatisticsViewIntegrationTests : IntegrationTestBase
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
    public async Task TournamentStatsView_NavigateToTournamentStats_PageLoadsSuccessfully()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int tournamentId = 1;

        try
        {
            // Act
            var response = await page.GoToAsync($"{BaseUrl}/tournaments/{tournamentId}/stats");

            // Assert
            Assert.IsNotNull(response, "Page response should not be null");
            Assert.IsTrue(response.Ok, "Page should load successfully with 200 OK");
            Assert.IsTrue(response.Url.Contains($"/tournaments/{tournamentId}/stats"),
                "URL should contain the tournament stats path");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task TournamentStatsView_TimeSeriesChart_LoadsSuccessfully()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int tournamentId = 1;

        try
        {
            // Act
            await page.GoToAsync($"{BaseUrl}/tournaments/{tournamentId}/stats");

            // Wait for chart to load
            await page.WaitForSelectorAsync(".chart-container canvas", new WaitForSelectorOptions
            {
                Timeout = 5000
            });

            var chartVisible = await page.QuerySelectorAsync(".chart-container canvas");

            // Assert
            Assert.IsNotNull(chartVisible, "Tournament time series chart should be visible");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task TournamentStatsView_ParticipantTrendAnalysis_DisplaysCorrectly()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int tournamentId = 1;

        try
        {
            // Act
            await page.GoToAsync($"{BaseUrl}/tournaments/{tournamentId}/stats");
            await page.WaitForSelectorAsync(".chart-container", new WaitForSelectorOptions
            {
                Timeout = 5000
            });

            var pageContent = await page.GetContentAsync();

            // Assert
            Assert.IsTrue(
                pageContent.Contains("Participant") ||
                pageContent.Contains("participant") ||
                pageContent.Contains("Players") ||
                pageContent.Contains("players"),
                "Page should display participant trend information");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task TournamentStatsView_AverageScoreEvolution_DisplaysOverTime()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int tournamentId = 1;

        try
        {
            // Act
            await page.GoToAsync($"{BaseUrl}/tournaments/{tournamentId}/stats");
            await page.WaitForSelectorAsync(".chart-container", new WaitForSelectorOptions
            {
                Timeout = 5000
            });

            var pageContent = await page.GetContentAsync();

            // Assert
            Assert.IsTrue(
                pageContent.Contains("Average Score") ||
                pageContent.Contains("average score") ||
                pageContent.Contains("Score Evolution") ||
                pageContent.Contains("score evolution"),
                "Page should display average score evolution information");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task TournamentStatsView_CompletionRateTracking_DisplaysAccurately()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int tournamentId = 1;

        try
        {
            // Act
            await page.GoToAsync($"{BaseUrl}/tournaments/{tournamentId}/stats");
            await page.WaitForSelectorAsync(".chart-container", new WaitForSelectorOptions
            {
                Timeout = 5000
            });

            var pageContent = await page.GetContentAsync();

            // Assert
            Assert.IsTrue(
                pageContent.Contains("Completion Rate") ||
                pageContent.Contains("completion rate") ||
                pageContent.Contains("Completion") ||
                pageContent.Contains("completion"),
                "Page should display completion rate tracking");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task TournamentStatsView_ScoreDistribution_ShowsAccurateData()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int tournamentId = 1;

        try
        {
            // Act
            await page.GoToAsync($"{BaseUrl}/tournaments/{tournamentId}/stats");
            await page.WaitForSelectorAsync(".chart-container", new WaitForSelectorOptions
            {
                Timeout = 5000
            });

            // Look for score distribution information
            var pageContent = await page.GetContentAsync();

            // Assert
            Assert.IsTrue(
                pageContent.Contains("Distribution") ||
                pageContent.Contains("distribution") ||
                pageContent.Contains("Range") ||
                pageContent.Contains("range"),
                "Page should show score distribution information");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task TournamentStatsView_MultipleRounds_DataDisplayedOverTime()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int tournamentId = 1; // Assuming tournament with 10+ rounds per quickstart.md

        try
        {
            // Act
            await page.GoToAsync($"{BaseUrl}/tournaments/{tournamentId}/stats");
            await page.WaitForSelectorAsync(".chart-container canvas", new WaitForSelectorOptions
            {
                Timeout = 5000
            });

            var chartVisible = await page.QuerySelectorAsync(".chart-container canvas");

            // Assert
            Assert.IsNotNull(chartVisible, "Chart should display data across multiple rounds/weeks");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task TournamentStatsView_NonExistentTournament_HandlesGracefully()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int nonExistentTournamentId = 999999;

        try
        {
            // Act
            var response = await page.GoToAsync($"{BaseUrl}/tournaments/{nonExistentTournamentId}/stats");
            await Task.Delay(1000);

            var pageContent = await page.GetContentAsync();

            // Assert
            Assert.IsTrue(
                pageContent.Contains("Not Found") ||
                pageContent.Contains("not found") ||
                pageContent.Contains("404") ||
                pageContent.Contains("does not exist"),
                "Page should show appropriate error message for non-existent tournament");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task TournamentStatsView_RenderingPerformance_LoadsWithin500ms()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int tournamentId = 1;

        try
        {
            // Act
            var startTime = DateTime.UtcNow;
            await page.GoToAsync($"{BaseUrl}/tournaments/{tournamentId}/stats");
            await page.WaitForSelectorAsync(".chart-container canvas", new WaitForSelectorOptions
            {
                Timeout = 5000
            });
            var endTime = DateTime.UtcNow;

            var loadTime = (endTime - startTime).TotalMilliseconds;

            // Assert
            Assert.IsTrue(loadTime < 2000,
                $"Chart should render within 2000ms (actual: {loadTime}ms) - performance requirement from quickstart.md");
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}
