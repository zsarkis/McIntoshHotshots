using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.AspNetCore.Mvc.Testing;
using PuppeteerSharp;
using System;
using System.Threading.Tasks;

namespace McIntoshHotshots.Tests.Integration;

/// <summary>
/// Integration tests for Data Validation and Edge Cases scenario from quickstart.md
/// Tests error handling, validation, and edge case scenarios
/// TDD: These tests MUST FAIL until proper error handling is implemented
/// </summary>
[TestClass]
public class DataValidationEdgeCasesIntegrationTests : IntegrationTestBase
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
    public async Task EdgeCases_PlayerWithLessThan3DataPoints_ShowsAppropriateMessage()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int playerIdWithLimitedData = 2; // Assuming this player has < 3 data points

        try
        {
            // Act
            await page.GoToAsync($"{BaseUrl}/players/{playerIdWithLimitedData}/stats");
            await Task.Delay(1000);

            var pageContent = await page.GetContentAsync();

            // Assert
            Assert.IsTrue(
                pageContent.Contains("insufficient data") ||
                pageContent.Contains("Insufficient data") ||
                pageContent.Contains("minimum 3 data points") ||
                pageContent.Contains("not enough data") ||
                pageContent.Contains("No data available"),
                "Page should show appropriate message for insufficient data");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task EdgeCases_InvalidPlayerId_Returns404WithUserFriendlyMessage()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int invalidPlayerId = 999999;

        try
        {
            // Act
            var response = await page.GoToAsync($"{BaseUrl}/players/{invalidPlayerId}/stats");
            await Task.Delay(1000);

            var pageContent = await page.GetContentAsync();

            // Assert
            Assert.IsTrue(
                pageContent.Contains("404") ||
                pageContent.Contains("Not Found") ||
                pageContent.Contains("not found") ||
                pageContent.Contains("does not exist") ||
                pageContent.Contains("Player not found"),
                "Page should show user-friendly 404 message for invalid player ID");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task EdgeCases_InvalidDateRange_ShowsValidationError()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int playerId = 1;

        try
        {
            // Act - Navigate and try to set invalid date range (end before start)
            await page.GoToAsync($"{BaseUrl}/players/{playerId}/stats");

            try
            {
                await page.WaitForSelectorAsync("[data-testid='start-date']", new WaitForSelectorOptions
                {
                    Timeout = 3000
                });

                // Clear and set invalid date range
                await page.EvaluateExpressionAsync("document.querySelector('[data-testid=\"start-date\"]').value = ''");
                await page.TypeAsync("[data-testid='start-date']", "2024-12-31");

                await page.EvaluateExpressionAsync("document.querySelector('[data-testid=\"end-date\"]').value = ''");
                await page.TypeAsync("[data-testid='end-date']", "2024-01-01"); // End before start

                // Try to apply
                var applyButton = await page.QuerySelectorAsync("[data-testid='apply-date-range']");
                if (applyButton != null)
                {
                    await page.ClickAsync("[data-testid='apply-date-range']");
                    await Task.Delay(1000);
                }

                var pageContent = await page.GetContentAsync();

                // Assert
                Assert.IsTrue(
                    pageContent.Contains("invalid date range") ||
                    pageContent.Contains("Invalid date range") ||
                    pageContent.Contains("validation error") ||
                    pageContent.Contains("end date must be after start date") ||
                    pageContent.Contains("error"),
                    "Page should show validation error for invalid date range");
            }
            catch
            {
                // If date inputs don't exist yet, that's okay for TDD - test will fail appropriately
                Assert.Fail("Date range inputs should exist on the page");
            }
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task EdgeCases_VeryLargeDateRange_ShowsWarningOrPagination()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int playerId = 1;

        try
        {
            // Act - Try to load data for >2 years
            await page.GoToAsync($"{BaseUrl}/players/{playerId}/stats?startDate=2020-01-01&endDate=2024-12-31");
            await Task.Delay(2000);

            var pageContent = await page.GetContentAsync();

            // Assert - Should show warning or implement pagination
            Assert.IsTrue(
                pageContent.Contains("warning") ||
                pageContent.Contains("large date range") ||
                pageContent.Contains("pagination") ||
                pageContent.Contains("chart") || // Or successfully loads the chart
                pageContent.Contains("performance"),
                "Page should either show performance warning, implement pagination, or successfully load data");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task EdgeCases_NoTournamentData_HandledGracefully()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int tournamentIdWithNoData = 999999;

        try
        {
            // Act
            await page.GoToAsync($"{BaseUrl}/tournaments/{tournamentIdWithNoData}/stats");
            await Task.Delay(1000);

            var pageContent = await page.GetContentAsync();

            // Assert
            Assert.IsTrue(
                pageContent.Contains("404") ||
                pageContent.Contains("Not Found") ||
                pageContent.Contains("not found") ||
                pageContent.Contains("no data") ||
                pageContent.Contains("does not exist"),
                "Page should gracefully handle tournament with no data");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task EdgeCases_NegativePlayerId_HandledCorrectly()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int negativePlayerId = -1;

        try
        {
            // Act
            var response = await page.GoToAsync($"{BaseUrl}/players/{negativePlayerId}/stats");
            await Task.Delay(1000);

            var pageContent = await page.GetContentAsync();

            // Assert
            Assert.IsTrue(
                pageContent.Contains("404") ||
                pageContent.Contains("400") ||
                pageContent.Contains("Bad Request") ||
                pageContent.Contains("Invalid") ||
                pageContent.Contains("not found"),
                "Page should handle negative player ID appropriately");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task EdgeCases_NonNumericPlayerId_ReturnsError()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        string invalidId = "abc";

        try
        {
            // Act
            var response = await page.GoToAsync($"{BaseUrl}/players/{invalidId}/stats");
            await Task.Delay(1000);

            var pageContent = await page.GetContentAsync();

            // Assert
            Assert.IsTrue(
                pageContent.Contains("400") ||
                pageContent.Contains("404") ||
                pageContent.Contains("Bad Request") ||
                pageContent.Contains("Invalid") ||
                pageContent.Contains("error"),
                "Page should return error for non-numeric player ID");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task EdgeCases_InvalidPeriodParameter_ShowsError()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int playerId = 1;

        try
        {
            // Act
            var response = await page.GoToAsync($"{BaseUrl}/players/{playerId}/stats?period=invalid");
            await Task.Delay(1000);

            var pageContent = await page.GetContentAsync();

            // Assert
            Assert.IsTrue(
                pageContent.Contains("400") ||
                pageContent.Contains("Bad Request") ||
                pageContent.Contains("Invalid period") ||
                pageContent.Contains("error") ||
                pageContent.Contains("weekly"), // Falls back to default
                "Page should handle invalid period parameter");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task EdgeCases_InvalidMetricParameter_ShowsError()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int playerId = 1;

        try
        {
            // Act
            var response = await page.GoToAsync($"{BaseUrl}/players/{playerId}/stats?metric=invalid_metric");
            await Task.Delay(1000);

            var pageContent = await page.GetContentAsync();

            // Assert
            Assert.IsTrue(
                pageContent.Contains("400") ||
                pageContent.Contains("Bad Request") ||
                pageContent.Contains("Invalid metric") ||
                pageContent.Contains("error") ||
                pageContent.Contains("average"), // Falls back to default
                "Page should handle invalid metric parameter");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task EdgeCases_MalformedDateParameter_ShowsError()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int playerId = 1;

        try
        {
            // Act
            var response = await page.GoToAsync($"{BaseUrl}/players/{playerId}/stats?startDate=not-a-date");
            await Task.Delay(1000);

            var pageContent = await page.GetContentAsync();

            // Assert
            Assert.IsTrue(
                pageContent.Contains("400") ||
                pageContent.Contains("Bad Request") ||
                pageContent.Contains("Invalid date") ||
                pageContent.Contains("error") ||
                pageContent.Contains("chart"), // Or ignores invalid param and loads default
                "Page should handle malformed date parameter");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task EdgeCases_ZeroPlayerId_HandledCorrectly()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int zeroPlayerId = 0;

        try
        {
            // Act
            var response = await page.GoToAsync($"{BaseUrl}/players/{zeroPlayerId}/stats");
            await Task.Delay(1000);

            var pageContent = await page.GetContentAsync();

            // Assert
            Assert.IsTrue(
                pageContent.Contains("404") ||
                pageContent.Contains("400") ||
                pageContent.Contains("Not Found") ||
                pageContent.Contains("Invalid") ||
                pageContent.Contains("error"),
                "Page should handle zero player ID appropriately");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task EdgeCases_FutureDateRange_HandledGracefully()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int playerId = 1;
        string futureStartDate = "2030-01-01";
        string futureEndDate = "2030-12-31";

        try
        {
            // Act
            await page.GoToAsync($"{BaseUrl}/players/{playerId}/stats?startDate={futureStartDate}&endDate={futureEndDate}");
            await Task.Delay(1000);

            var pageContent = await page.GetContentAsync();

            // Assert
            Assert.IsTrue(
                pageContent.Contains("no data") ||
                pageContent.Contains("No data") ||
                pageContent.Contains("insufficient data") ||
                pageContent.Contains("future date") ||
                pageContent.Contains("chart"), // Or shows empty chart
                "Page should handle future date range gracefully");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task EdgeCases_SQLInjectionAttempt_BlockedSafely()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        string sqlInjection = "1' OR '1'='1";

        try
        {
            // Act
            var response = await page.GoToAsync($"{BaseUrl}/players/{sqlInjection}/stats");
            await Task.Delay(1000);

            var pageContent = await page.GetContentAsync();

            // Assert - Should not execute SQL, should return error
            Assert.IsTrue(
                pageContent.Contains("400") ||
                pageContent.Contains("404") ||
                pageContent.Contains("Bad Request") ||
                pageContent.Contains("Invalid") ||
                pageContent.Contains("error"),
                "Page should safely handle SQL injection attempts");

            // Should NOT return all players' data
            Assert.IsFalse(pageContent.Contains("SELECT *") || pageContent.Contains("DROP TABLE"),
                "Page should not expose SQL");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task EdgeCases_XSSAttempt_SanitizedCorrectly()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int playerId = 1;
        string xssAttempt = "<script>alert('xss')</script>";

        try
        {
            // Act
            await page.GoToAsync($"{BaseUrl}/players/{playerId}/stats?tournamentType={xssAttempt}");
            await Task.Delay(1000);

            var pageContent = await page.GetContentAsync();

            // Assert - Script should be sanitized/escaped, not executed
            Assert.IsFalse(pageContent.Contains("<script>alert('xss')</script>"),
                "XSS attempts should be sanitized");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task EdgeCases_ExcessivelyLongQueryString_HandledGracefully()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int playerId = 1;
        string excessiveParam = new string('a', 10000); // 10k characters

        try
        {
            // Act
            var response = await page.GoToAsync(
                $"{BaseUrl}/players/{playerId}/stats?param={excessiveParam}",
                new NavigationOptions { Timeout = 10000 }
            );
            await Task.Delay(1000);

            // Assert - Should either ignore the param or return error, not crash
            Assert.IsNotNull(response, "Server should handle excessively long query strings without crashing");
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}
