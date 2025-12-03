using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;

namespace McIntoshHotshots.Tests.Contract;

/// <summary>
/// Contract tests for GET /api/stats/player/{playerId}/metrics endpoint
/// These tests validate the API contract matches the OpenAPI specification
/// TDD: These tests should FAIL until the controller is implemented
/// </summary>
[TestClass]
public class PlayerMetricsContractTests : ContractTestBase
{
    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        InitializeTestFactory();
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        CleanupTestFactory();
    }

    [TestMethod]
    public async Task GetPlayerMetrics_ValidPlayerId_Returns200WithPlayerMetricsResponse()
    {
        // Arrange
        int playerId = 1;

        // Act
        var response = await Client!.GetAsync($"/api/stats/player/{playerId}/metrics");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("application/json", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        // Validate PlayerMetricsResponse contract
        Assert.IsTrue(root.TryGetProperty("playerId", out _), "Response should contain 'playerId'");
        Assert.IsTrue(root.TryGetProperty("playerName", out _), "Response should contain 'playerName'");
        Assert.IsTrue(root.TryGetProperty("currentMetrics", out _), "Response should contain 'currentMetrics'");
        Assert.IsTrue(root.TryGetProperty("calculatedAt", out _), "Response should contain 'calculatedAt'");
        // trends is nullable and only present when includeTrends=true
    }

    [TestMethod]
    public async Task GetPlayerMetrics_CurrentMetricsStructure_MatchesContract()
    {
        // Arrange
        int playerId = 1;

        // Act
        var response = await Client!.GetAsync($"/api/stats/player/{playerId}/metrics");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        // Validate CurrentMetrics contract
        Assert.IsTrue(root.TryGetProperty("currentMetrics", out var metrics));
        Assert.IsTrue(metrics.TryGetProperty("averageScore", out _), "CurrentMetrics should contain 'averageScore'");
        Assert.IsTrue(metrics.TryGetProperty("medianScore", out _), "CurrentMetrics should contain 'medianScore'");
        Assert.IsTrue(metrics.TryGetProperty("completionRate", out _), "CurrentMetrics should contain 'completionRate'");
        Assert.IsTrue(metrics.TryGetProperty("checkoutPercentage", out _), "CurrentMetrics should contain 'checkoutPercentage'");
        Assert.IsTrue(metrics.TryGetProperty("doublesHitRate", out _), "CurrentMetrics should contain 'doublesHitRate'");
        Assert.IsTrue(metrics.TryGetProperty("gamesPlayed", out _), "CurrentMetrics should contain 'gamesPlayed'");
        Assert.IsTrue(metrics.TryGetProperty("tournamentsParticipated", out _), "CurrentMetrics should contain 'tournamentsParticipated'");
    }

    [TestMethod]
    public async Task GetPlayerMetrics_WithIncludeTrendsFalse_DoesNotIncludeTrends()
    {
        // Arrange
        int playerId = 1;

        // Act
        var response = await Client!.GetAsync($"/api/stats/player/{playerId}/metrics?includeTrends=false");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        // Trends should be null or absent when includeTrends is false
        if (root.TryGetProperty("trends", out var trends))
        {
            Assert.AreEqual(JsonValueKind.Null, trends.ValueKind,
                "trends should be null when includeTrends is false");
        }
    }

    [TestMethod]
    public async Task GetPlayerMetrics_WithIncludeTrendsTrue_IncludesTrendAnalysis()
    {
        // Arrange
        int playerId = 1;

        // Act
        var response = await Client!.GetAsync($"/api/stats/player/{playerId}/metrics?includeTrends=true");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        // Trends should be present and non-null when includeTrends is true (if data available)
        Assert.IsTrue(root.TryGetProperty("trends", out var trends), "Response should contain 'trends' property");

        // If trends data is available (not null), validate TrendAnalysis contract
        if (trends.ValueKind != JsonValueKind.Null)
        {
            Assert.IsTrue(trends.TryGetProperty("scoreTrend", out var scoreTrend), "TrendAnalysis should contain 'scoreTrend'");

            // Validate scoreTrend enum value
            var scoreTrendValue = scoreTrend.GetString();
            Assert.IsTrue(
                scoreTrendValue == "improving" || scoreTrendValue == "declining" || scoreTrendValue == "stable",
                $"scoreTrend should be 'improving', 'declining', or 'stable', but was '{scoreTrendValue}'");

            Assert.IsTrue(trends.TryGetProperty("trendConfidence", out _), "TrendAnalysis should contain 'trendConfidence'");
            // improvementRate is nullable
        }
    }

    [TestMethod]
    public async Task GetPlayerMetrics_DefaultIncludeTrendsIsFalse()
    {
        // Arrange
        int playerId = 1;

        // Act - No includeTrends parameter specified
        var response = await Client!.GetAsync($"/api/stats/player/{playerId}/metrics");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        // Default should be false, so trends should be null or absent
        if (root.TryGetProperty("trends", out var trends))
        {
            Assert.AreEqual(JsonValueKind.Null, trends.ValueKind,
                "trends should be null by default (includeTrends defaults to false)");
        }
    }

    [TestMethod]
    public async Task GetPlayerMetrics_NonExistentPlayer_Returns404NotFound()
    {
        // Arrange
        int nonExistentPlayerId = 999999;

        // Act
        var response = await Client!.GetAsync($"/api/stats/player/{nonExistentPlayerId}/metrics");

        // Assert
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        // Verify ErrorResponse contract
        Assert.IsTrue(root.TryGetProperty("error", out _), "Error response should contain 'error'");
        Assert.IsTrue(root.TryGetProperty("message", out _), "Error response should contain 'message'");
        Assert.IsTrue(root.TryGetProperty("timestamp", out _), "Error response should contain 'timestamp'");
        Assert.IsTrue(root.TryGetProperty("path", out _), "Error response should contain 'path'");
    }

    [TestMethod]
    public async Task GetPlayerMetrics_PlayerIdDataTypes_AreCorrect()
    {
        // Arrange
        int playerId = 1;

        // Act
        var response = await Client!.GetAsync($"/api/stats/player/{playerId}/metrics");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        // Verify playerId is integer
        Assert.IsTrue(root.TryGetProperty("playerId", out var playerIdProp));
        Assert.AreEqual(JsonValueKind.Number, playerIdProp.ValueKind, "playerId should be a number");

        // Verify playerName is string
        Assert.IsTrue(root.TryGetProperty("playerName", out var playerName));
        Assert.AreEqual(JsonValueKind.String, playerName.ValueKind, "playerName should be a string");

        // Verify calculatedAt is string (date-time)
        Assert.IsTrue(root.TryGetProperty("calculatedAt", out var calculatedAt));
        Assert.AreEqual(JsonValueKind.String, calculatedAt.ValueKind, "calculatedAt should be a string (ISO 8601 date-time)");
    }

    [TestMethod]
    public async Task GetPlayerMetrics_NumericMetrics_AreNumbers()
    {
        // Arrange
        int playerId = 1;

        // Act
        var response = await Client!.GetAsync($"/api/stats/player/{playerId}/metrics");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        Assert.IsTrue(root.TryGetProperty("currentMetrics", out var metrics));

        // Verify all numeric fields are numbers
        Assert.IsTrue(metrics.TryGetProperty("averageScore", out var avgScore));
        Assert.AreEqual(JsonValueKind.Number, avgScore.ValueKind, "averageScore should be a number");

        Assert.IsTrue(metrics.TryGetProperty("medianScore", out var medScore));
        Assert.AreEqual(JsonValueKind.Number, medScore.ValueKind, "medianScore should be a number");

        Assert.IsTrue(metrics.TryGetProperty("completionRate", out var compRate));
        Assert.AreEqual(JsonValueKind.Number, compRate.ValueKind, "completionRate should be a number");

        Assert.IsTrue(metrics.TryGetProperty("checkoutPercentage", out var checkoutPct));
        Assert.AreEqual(JsonValueKind.Number, checkoutPct.ValueKind, "checkoutPercentage should be a number");

        Assert.IsTrue(metrics.TryGetProperty("doublesHitRate", out var doublesRate));
        Assert.AreEqual(JsonValueKind.Number, doublesRate.ValueKind, "doublesHitRate should be a number");

        Assert.IsTrue(metrics.TryGetProperty("gamesPlayed", out var games));
        Assert.AreEqual(JsonValueKind.Number, games.ValueKind, "gamesPlayed should be a number");

        Assert.IsTrue(metrics.TryGetProperty("tournamentsParticipated", out var tournaments));
        Assert.AreEqual(JsonValueKind.Number, tournaments.ValueKind, "tournamentsParticipated should be a number");
    }

    [TestMethod]
    public async Task GetPlayerMetrics_TrendConfidence_IsBetween0And1()
    {
        // Arrange
        int playerId = 1;

        // Act
        var response = await Client!.GetAsync($"/api/stats/player/{playerId}/metrics?includeTrends=true");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        if (root.TryGetProperty("trends", out var trends) && trends.ValueKind != JsonValueKind.Null)
        {
            Assert.IsTrue(trends.TryGetProperty("trendConfidence", out var confidence));

            var confidenceValue = confidence.GetDecimal();
            Assert.IsTrue(confidenceValue >= 0 && confidenceValue <= 1,
                $"trendConfidence should be between 0 and 1, but was {confidenceValue}");
        }
    }

    [TestMethod]
    public async Task GetPlayerMetrics_InvalidBooleanParameter_Returns400BadRequest()
    {
        // Arrange
        int playerId = 1;
        string invalidBoolean = "notABoolean";

        // Act
        var response = await Client!.GetAsync($"/api/stats/player/{playerId}/metrics?includeTrends={invalidBoolean}");

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        // Verify ErrorResponse contract
        Assert.IsTrue(root.TryGetProperty("error", out _));
        Assert.IsTrue(root.TryGetProperty("message", out _));
    }

    [TestMethod]
    public async Task GetPlayerMetrics_CalculatedAtTimestamp_IsValidISO8601()
    {
        // Arrange
        int playerId = 1;

        // Act
        var response = await Client!.GetAsync($"/api/stats/player/{playerId}/metrics");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        Assert.IsTrue(root.TryGetProperty("calculatedAt", out var calculatedAt));

        var timestampString = calculatedAt.GetString();
        Assert.IsNotNull(timestampString, "calculatedAt should not be null");

        // Verify it can be parsed as DateTime (ISO 8601 format)
        bool isValidDateTime = DateTime.TryParse(timestampString, out _);
        Assert.IsTrue(isValidDateTime, $"calculatedAt '{timestampString}' should be a valid ISO 8601 date-time");
    }
}
