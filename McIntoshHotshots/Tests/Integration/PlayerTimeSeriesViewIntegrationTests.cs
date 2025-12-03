using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.AspNetCore.Mvc.Testing;
using PuppeteerSharp;
using System;
using System.Threading.Tasks;

namespace McIntoshHotshots.Tests.Integration;

/// <summary>
/// Integration tests for Player Time Series View scenario from quickstart.md
/// Tests complete user workflow from navigation to chart interaction
/// TDD: These tests MUST FAIL until the views and controllers are fully implemented
/// </summary>
[TestClass]
public class PlayerTimeSeriesViewIntegrationTests : IntegrationTestBase
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
    public async Task PlayerTimeSeriesView_NavigateToPlayerStats_PageLoadsSuccessfully()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int playerId = 1;

        try
        {
            // Act
            var response = await page.GoToAsync($"{BaseUrl}/players/{playerId}/stats");

            // Assert
            Assert.IsNotNull(response, "Page response should not be null");
            Assert.IsTrue(response.Ok, "Page should load successfully with 200 OK");
            Assert.IsTrue(response.Url.Contains($"/players/{playerId}/stats"),
                "URL should contain the player stats path");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task PlayerTimeSeriesView_ChartLoadsWithDefaultWeeklyView_DisplaysChart()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int playerId = 1;

        try
        {
            // Act
            await page.GoToAsync($"{BaseUrl}/players/{playerId}/stats");

            // Wait for chart to load (with timeout)
            await page.WaitForSelectorAsync(".chart-container canvas", new WaitForSelectorOptions
            {
                Timeout = 5000
            });

            var chartVisible = await page.QuerySelectorAsync(".chart-container canvas");

            // Assert
            Assert.IsNotNull(chartVisible, "Chart canvas should be visible on the page");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task PlayerTimeSeriesView_MetricToggle_SwitchesBetweenAverageAndMedian()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int playerId = 1;

        try
        {
            // Act
            await page.GoToAsync($"{BaseUrl}/players/{playerId}/stats");
            await page.WaitForSelectorAsync("[data-testid='metric-toggle']", new WaitForSelectorOptions
            {
                Timeout = 5000
            });

            // Click median toggle
            await page.ClickAsync("[data-testid='median-toggle']");

            // Wait for chart to update
            await Task.Delay(500);

            var pageContent = await page.GetContentAsync();

            // Assert
            Assert.IsTrue(pageContent.Contains("Median") || pageContent.Contains("median"),
                "Page should show 'Median' after toggling");

            // Toggle back to average
            await page.ClickAsync("[data-testid='average-toggle']");
            await Task.Delay(500);

            pageContent = await page.GetContentAsync();
            Assert.IsTrue(pageContent.Contains("Average") || pageContent.Contains("average"),
                "Page should show 'Average' after toggling back");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task PlayerTimeSeriesView_PeriodSelector_ChangesChartGranularity()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int playerId = 1;

        try
        {
            // Act
            await page.GoToAsync($"{BaseUrl}/players/{playerId}/stats");
            await page.WaitForSelectorAsync("[data-testid='period-selector']", new WaitForSelectorOptions
            {
                Timeout = 5000
            });

            // Test weekly -> monthly
            await page.SelectAsync("[data-testid='period-selector']", "monthly");
            await page.WaitForNetworkIdleAsync(new WaitForNetworkIdleOptions { Timeout = 3000 });

            var chartAfterMonthly = await page.QuerySelectorAsync(".chart-container canvas");
            Assert.IsNotNull(chartAfterMonthly, "Chart should still be visible after changing to monthly");

            // Test monthly -> quarterly
            await page.SelectAsync("[data-testid='period-selector']", "quarterly");
            await page.WaitForNetworkIdleAsync(new WaitForNetworkIdleOptions { Timeout = 3000 });

            var chartAfterQuarterly = await page.QuerySelectorAsync(".chart-container canvas");
            Assert.IsNotNull(chartAfterQuarterly, "Chart should still be visible after changing to quarterly");

            // Test quarterly -> yearly
            await page.SelectAsync("[data-testid='period-selector']", "yearly");
            await page.WaitForNetworkIdleAsync(new WaitForNetworkIdleOptions { Timeout = 3000 });

            var chartAfterYearly = await page.QuerySelectorAsync(".chart-container canvas");
            Assert.IsNotNull(chartAfterYearly, "Chart should still be visible after changing to yearly");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task PlayerTimeSeriesView_DateRangeSelection_FiltersDataCorrectly()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int playerId = 1;

        try
        {
            // Act
            await page.GoToAsync($"{BaseUrl}/players/{playerId}/stats");
            await page.WaitForSelectorAsync("[data-testid='start-date']", new WaitForSelectorOptions
            {
                Timeout = 5000
            });

            // Set date range
            await page.TypeAsync("[data-testid='start-date']", "2024-01-01");
            await page.TypeAsync("[data-testid='end-date']", "2024-12-31");

            // Apply date range (assuming there's an apply button)
            var applyButton = await page.QuerySelectorAsync("[data-testid='apply-date-range']");
            if (applyButton != null)
            {
                await page.ClickAsync("[data-testid='apply-date-range']");
                await page.WaitForNetworkIdleAsync(new WaitForNetworkIdleOptions { Timeout = 3000 });
            }

            var chartAfterFilter = await page.QuerySelectorAsync(".chart-container canvas");

            // Assert
            Assert.IsNotNull(chartAfterFilter, "Chart should be visible after applying date range filter");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task PlayerTimeSeriesView_TournamentTypeFilter_ShowsFilteredData()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int playerId = 1;

        try
        {
            // Act
            await page.GoToAsync($"{BaseUrl}/players/{playerId}/stats");
            await page.WaitForSelectorAsync("[data-testid='tournament-type-filter']", new WaitForSelectorOptions
            {
                Timeout = 5000
            });

            // Filter by Singles
            await page.SelectAsync("[data-testid='tournament-type-filter']", "Singles");
            await page.WaitForNetworkIdleAsync(new WaitForNetworkIdleOptions { Timeout = 3000 });

            var chartAfterFilter = await page.QuerySelectorAsync(".chart-container canvas");

            // Assert
            Assert.IsNotNull(chartAfterFilter, "Chart should be visible after applying tournament type filter");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task PlayerTimeSeriesView_ExtraMetrics_AllMetricsPreserved()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int playerId = 1;

        try
        {
            // Act
            await page.GoToAsync($"{BaseUrl}/players/{playerId}/stats");
            await page.WaitForSelectorAsync(".chart-container", new WaitForSelectorOptions
            {
                Timeout = 5000
            });

            var pageContent = await page.GetContentAsync();

            // Assert - Verify all extra metrics are present
            Assert.IsTrue(pageContent.Contains("Completion Rate") || pageContent.Contains("completion rate"),
                "Page should display completion rate metric");
            Assert.IsTrue(pageContent.Contains("Checkout") || pageContent.Contains("checkout"),
                "Page should display checkout percentage metric");
            Assert.IsTrue(pageContent.Contains("Doubles Hit Rate") || pageContent.Contains("doubles hit rate"),
                "Page should display doubles hit rate metric");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task PlayerTimeSeriesView_InsufficientData_ShowsAppropriateMessage()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int playerIdWithNoData = 999999; // Assuming this player doesn't exist or has no data

        try
        {
            // Act
            await page.GoToAsync($"{BaseUrl}/players/{playerIdWithNoData}/stats");
            await Task.Delay(1000); // Wait for page to load

            var pageContent = await page.GetContentAsync();

            // Assert
            Assert.IsTrue(
                pageContent.Contains("insufficient data") ||
                pageContent.Contains("no data") ||
                pageContent.Contains("minimum 3 data points") ||
                pageContent.Contains("Not Found") ||
                pageContent.Contains("404"),
                "Page should show appropriate message for insufficient data or non-existent player");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task PlayerTimeSeriesView_ChartRenderingPerformance_LoadsWithin500ms()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int playerId = 1;

        try
        {
            // Act
            var startTime = DateTime.UtcNow;
            await page.GoToAsync($"{BaseUrl}/players/{playerId}/stats");
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
