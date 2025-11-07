using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;

namespace McIntoshHotshots.Tests.Contract;

/// <summary>
/// Contract tests for GET /api/stats/tournament/{tournamentId}/timeseries endpoint
/// These tests validate the API contract matches the OpenAPI specification
/// TDD: These tests should FAIL until the controller is implemented
/// </summary>
[TestClass]
public class TournamentTimeSeriesContractTests
{
    private static WebApplicationFactory<Program>? _factory;
    private static HttpClient? _client;

    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [TestMethod]
    public async Task GetTournamentTimeSeries_ValidTournamentId_Returns200WithTimeSeriesResponse()
    {
        // Arrange
        int tournamentId = 1;

        // Act
        var response = await _client!.GetAsync($"/api/stats/tournament/{tournamentId}/timeseries");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("application/json", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        // Validate TimeSeriesResponse contract
        Assert.IsTrue(root.TryGetProperty("tournamentId", out _), "Response should contain 'tournamentId'");
        Assert.IsTrue(root.TryGetProperty("period", out _), "Response should contain 'period'");
        Assert.IsTrue(root.TryGetProperty("metric", out _), "Response should contain 'metric'");
        Assert.IsTrue(root.TryGetProperty("dataPoints", out var dataPoints), "Response should contain 'dataPoints'");
        Assert.AreEqual(JsonValueKind.Array, dataPoints.ValueKind, "dataPoints should be an array");
        Assert.IsTrue(root.TryGetProperty("metadata", out _), "Response should contain 'metadata'");
    }

    [TestMethod]
    public async Task GetTournamentTimeSeries_WithPeriodParameter_Returns200()
    {
        // Arrange
        int tournamentId = 1;
        string period = "weekly";

        // Act
        var response = await _client!.GetAsync(
            $"/api/stats/tournament/{tournamentId}/timeseries?period={period}");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        // Verify period in response matches request
        Assert.IsTrue(root.TryGetProperty("period", out var periodProperty));
        Assert.AreEqual(period, periodProperty.GetString());
    }

    [TestMethod]
    public async Task GetTournamentTimeSeries_WithMetricParameter_Returns200()
    {
        // Arrange
        int tournamentId = 1;
        string metric = "participant_count";

        // Act
        var response = await _client!.GetAsync(
            $"/api/stats/tournament/{tournamentId}/timeseries?metric={metric}");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        // Verify metric in response
        Assert.IsTrue(root.TryGetProperty("metric", out var metricProperty));
        Assert.AreEqual(metric, metricProperty.GetString());
    }

    [TestMethod]
    public async Task GetTournamentTimeSeries_WithAllValidMetrics_Returns200()
    {
        // Arrange - Test all valid metrics from OpenAPI spec
        int tournamentId = 1;
        string[] validMetrics = { "average_score", "median_score", "participant_count", "completion_rate" };

        foreach (var metric in validMetrics)
        {
            // Act
            var response = await _client!.GetAsync(
                $"/api/stats/tournament/{tournamentId}/timeseries?metric={metric}");

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
                $"Metric '{metric}' should return 200 OK");

            var content = await response.Content.ReadAsStringAsync();
            var jsonDocument = JsonDocument.Parse(content);
            var root = jsonDocument.RootElement;

            Assert.IsTrue(root.TryGetProperty("metric", out var metricProperty));
            Assert.AreEqual(metric, metricProperty.GetString());
        }
    }

    [TestMethod]
    public async Task GetTournamentTimeSeries_WithAllValidPeriods_Returns200()
    {
        // Arrange - Test all valid periods from OpenAPI spec
        int tournamentId = 1;
        string[] validPeriods = { "daily", "weekly", "monthly" };

        foreach (var period in validPeriods)
        {
            // Act
            var response = await _client!.GetAsync(
                $"/api/stats/tournament/{tournamentId}/timeseries?period={period}");

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
                $"Period '{period}' should return 200 OK");

            var content = await response.Content.ReadAsStringAsync();
            var jsonDocument = JsonDocument.Parse(content);
            var root = jsonDocument.RootElement;

            Assert.IsTrue(root.TryGetProperty("period", out var periodProperty));
            Assert.AreEqual(period, periodProperty.GetString());
        }
    }

    [TestMethod]
    public async Task GetTournamentTimeSeries_InvalidPeriod_Returns400BadRequest()
    {
        // Arrange
        int tournamentId = 1;
        string invalidPeriod = "yearly"; // Not valid for tournament timeseries

        // Act
        var response = await _client!.GetAsync(
            $"/api/stats/tournament/{tournamentId}/timeseries?period={invalidPeriod}");

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        // Verify ErrorResponse contract
        Assert.IsTrue(root.TryGetProperty("error", out _), "Error response should contain 'error'");
        Assert.IsTrue(root.TryGetProperty("message", out _), "Error response should contain 'message'");
    }

    [TestMethod]
    public async Task GetTournamentTimeSeries_InvalidMetric_Returns400BadRequest()
    {
        // Arrange
        int tournamentId = 1;
        string invalidMetric = "invalid_metric";

        // Act
        var response = await _client!.GetAsync(
            $"/api/stats/tournament/{tournamentId}/timeseries?metric={invalidMetric}");

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task GetTournamentTimeSeries_NonExistentTournament_Returns404NotFound()
    {
        // Arrange
        int nonExistentTournamentId = 999999;

        // Act
        var response = await _client!.GetAsync(
            $"/api/stats/tournament/{nonExistentTournamentId}/timeseries");

        // Assert
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        // Verify ErrorResponse contract
        Assert.IsTrue(root.TryGetProperty("error", out _));
        Assert.IsTrue(root.TryGetProperty("message", out _));
        Assert.IsTrue(root.TryGetProperty("timestamp", out _));
        Assert.IsTrue(root.TryGetProperty("path", out _));
    }

    [TestMethod]
    public async Task GetTournamentTimeSeries_DataPointsStructure_MatchesContract()
    {
        // Arrange
        int tournamentId = 1;

        // Act
        var response = await _client!.GetAsync($"/api/stats/tournament/{tournamentId}/timeseries");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        Assert.IsTrue(root.TryGetProperty("dataPoints", out var dataPoints));

        // If there are data points, validate their structure
        if (dataPoints.GetArrayLength() > 0)
        {
            var firstDataPoint = dataPoints[0];

            // Verify TimeSeriesDataPoint contract
            Assert.IsTrue(firstDataPoint.TryGetProperty("date", out _), "DataPoint should contain 'date'");
            Assert.IsTrue(firstDataPoint.TryGetProperty("value", out _), "DataPoint should contain 'value'");
            Assert.IsTrue(firstDataPoint.TryGetProperty("count", out _), "DataPoint should contain 'count'");
        }
    }

    [TestMethod]
    public async Task GetTournamentTimeSeries_MetadataStructure_MatchesContract()
    {
        // Arrange
        int tournamentId = 1;

        // Act
        var response = await _client!.GetAsync($"/api/stats/tournament/{tournamentId}/timeseries");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        Assert.IsTrue(root.TryGetProperty("metadata", out var metadata));

        // Verify TimeSeriesMetadata contract
        Assert.IsTrue(metadata.TryGetProperty("totalDataPoints", out _), "Metadata should contain 'totalDataPoints'");
        Assert.IsTrue(metadata.TryGetProperty("hasInsufficientData", out _), "Metadata should contain 'hasInsufficientData'");
        Assert.IsTrue(metadata.TryGetProperty("calculatedAt", out _), "Metadata should contain 'calculatedAt'");
    }

    [TestMethod]
    public async Task GetTournamentTimeSeries_DefaultPeriodIsDaily()
    {
        // Arrange
        int tournamentId = 1;

        // Act - No period parameter specified
        var response = await _client!.GetAsync($"/api/stats/tournament/{tournamentId}/timeseries");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        // Default period should be "daily" per OpenAPI spec (different from player endpoint)
        Assert.IsTrue(root.TryGetProperty("period", out var period));
        Assert.AreEqual("daily", period.GetString());
    }

    [TestMethod]
    public async Task GetTournamentTimeSeries_DefaultMetricIsAverageScore()
    {
        // Arrange
        int tournamentId = 1;

        // Act - No metric parameter specified
        var response = await _client!.GetAsync($"/api/stats/tournament/{tournamentId}/timeseries");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        // Default metric should be "average_score" per OpenAPI spec
        Assert.IsTrue(root.TryGetProperty("metric", out var metric));
        Assert.AreEqual("average_score", metric.GetString());
    }

    [TestMethod]
    public async Task GetTournamentTimeSeries_ResponseHasTournamentIdNotPlayerId()
    {
        // Arrange
        int tournamentId = 1;

        // Act
        var response = await _client!.GetAsync($"/api/stats/tournament/{tournamentId}/timeseries");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        // Should have tournamentId, not playerId
        Assert.IsTrue(root.TryGetProperty("tournamentId", out var tournamentIdProp));
        Assert.IsFalse(root.TryGetProperty("playerId", out _) &&
                      root.GetProperty("playerId").ValueKind != JsonValueKind.Null,
                      "Response should not have non-null playerId for tournament endpoint");
    }
}
