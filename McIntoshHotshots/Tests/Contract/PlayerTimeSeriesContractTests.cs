using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;

namespace McIntoshHotshots.Tests.Contract;

/// <summary>
/// Contract tests for GET /api/stats/player/{playerId}/timeseries endpoint
/// These tests validate the API contract matches the OpenAPI specification
/// TDD: These tests should FAIL until the controller is implemented
/// </summary>
[TestClass]
public class PlayerTimeSeriesContractTests
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
    public async Task GetPlayerTimeSeries_ValidPlayerId_Returns200WithTimeSeriesResponse()
    {
        // Arrange
        int playerId = 1;

        // Act
        var response = await _client!.GetAsync($"/api/stats/player/{playerId}/timeseries");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("application/json", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        // Validate TimeSeriesResponse contract
        Assert.IsTrue(root.TryGetProperty("playerId", out _), "Response should contain 'playerId'");
        Assert.IsTrue(root.TryGetProperty("period", out _), "Response should contain 'period'");
        Assert.IsTrue(root.TryGetProperty("metric", out _), "Response should contain 'metric'");
        Assert.IsTrue(root.TryGetProperty("dataPoints", out var dataPoints), "Response should contain 'dataPoints'");
        Assert.AreEqual(JsonValueKind.Array, dataPoints.ValueKind, "dataPoints should be an array");
        Assert.IsTrue(root.TryGetProperty("metadata", out _), "Response should contain 'metadata'");
    }

    [TestMethod]
    public async Task GetPlayerTimeSeries_WithPeriodParameter_Returns200()
    {
        // Arrange
        int playerId = 1;
        string period = "monthly";

        // Act
        var response = await _client!.GetAsync($"/api/stats/player/{playerId}/timeseries?period={period}");

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
    public async Task GetPlayerTimeSeries_WithMetricParameter_Returns200()
    {
        // Arrange
        int playerId = 1;
        string metric = "completion_rate";

        // Act
        var response = await _client!.GetAsync($"/api/stats/player/{playerId}/timeseries?metric={metric}");

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
    public async Task GetPlayerTimeSeries_WithDateRange_Returns200()
    {
        // Arrange
        int playerId = 1;
        string startDate = "2024-01-01";
        string endDate = "2024-12-31";

        // Act
        var response = await _client!.GetAsync(
            $"/api/stats/player/{playerId}/timeseries?startDate={startDate}&endDate={endDate}");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        // Verify date range in response
        Assert.IsTrue(root.TryGetProperty("startDate", out _));
        Assert.IsTrue(root.TryGetProperty("endDate", out _));
    }

    [TestMethod]
    public async Task GetPlayerTimeSeries_WithTournamentTypeFilter_Returns200()
    {
        // Arrange
        int playerId = 1;
        string tournamentType = "Singles";

        // Act
        var response = await _client!.GetAsync(
            $"/api/stats/player/{playerId}/timeseries?tournamentType={tournamentType}");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task GetPlayerTimeSeries_WithAllParameters_Returns200()
    {
        // Arrange
        int playerId = 1;
        string period = "quarterly";
        string metric = "doubles_hit_rate";
        string startDate = "2024-01-01";
        string endDate = "2024-12-31";
        string tournamentType = "Doubles";

        // Act
        var response = await _client!.GetAsync(
            $"/api/stats/player/{playerId}/timeseries?period={period}&metric={metric}&startDate={startDate}&endDate={endDate}&tournamentType={tournamentType}");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        // Verify all parameters reflected in response
        Assert.IsTrue(root.TryGetProperty("period", out var periodProp));
        Assert.AreEqual(period, periodProp.GetString());
        Assert.IsTrue(root.TryGetProperty("metric", out var metricProp));
        Assert.AreEqual(metric, metricProp.GetString());
    }

    [TestMethod]
    public async Task GetPlayerTimeSeries_InvalidPeriod_Returns400BadRequest()
    {
        // Arrange
        int playerId = 1;
        string invalidPeriod = "invalidPeriod";

        // Act
        var response = await _client!.GetAsync(
            $"/api/stats/player/{playerId}/timeseries?period={invalidPeriod}");

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

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
    public async Task GetPlayerTimeSeries_InvalidMetric_Returns400BadRequest()
    {
        // Arrange
        int playerId = 1;
        string invalidMetric = "invalidMetric";

        // Act
        var response = await _client!.GetAsync(
            $"/api/stats/player/{playerId}/timeseries?metric={invalidMetric}");

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task GetPlayerTimeSeries_NonExistentPlayer_Returns404NotFound()
    {
        // Arrange
        int nonExistentPlayerId = 999999;

        // Act
        var response = await _client!.GetAsync(
            $"/api/stats/player/{nonExistentPlayerId}/timeseries");

        // Assert
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        // Verify ErrorResponse contract
        Assert.IsTrue(root.TryGetProperty("error", out _));
        Assert.IsTrue(root.TryGetProperty("message", out _));
    }

    [TestMethod]
    public async Task GetPlayerTimeSeries_InvalidDateFormat_Returns400BadRequest()
    {
        // Arrange
        int playerId = 1;
        string invalidDate = "not-a-date";

        // Act
        var response = await _client!.GetAsync(
            $"/api/stats/player/{playerId}/timeseries?startDate={invalidDate}");

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task GetPlayerTimeSeries_DataPointsHaveRequiredProperties()
    {
        // Arrange
        int playerId = 1;

        // Act
        var response = await _client!.GetAsync($"/api/stats/player/{playerId}/timeseries");

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
            // standardDeviation, minValue, maxValue are nullable, so they might not be present
        }
    }

    [TestMethod]
    public async Task GetPlayerTimeSeries_MetadataHasRequiredProperties()
    {
        // Arrange
        int playerId = 1;

        // Act
        var response = await _client!.GetAsync($"/api/stats/player/{playerId}/timeseries");

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
        // warningMessage is nullable
    }

    [TestMethod]
    public async Task GetPlayerTimeSeries_DefaultPeriodIsWeekly()
    {
        // Arrange
        int playerId = 1;

        // Act - No period parameter specified
        var response = await _client!.GetAsync($"/api/stats/player/{playerId}/timeseries");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        // Default period should be "weekly" per OpenAPI spec
        Assert.IsTrue(root.TryGetProperty("period", out var period));
        Assert.AreEqual("weekly", period.GetString());
    }

    [TestMethod]
    public async Task GetPlayerTimeSeries_DefaultMetricIsAverage()
    {
        // Arrange
        int playerId = 1;

        // Act - No metric parameter specified
        var response = await _client!.GetAsync($"/api/stats/player/{playerId}/timeseries");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        // Default metric should be "average" per OpenAPI spec
        Assert.IsTrue(root.TryGetProperty("metric", out var metric));
        Assert.AreEqual("average", metric.GetString());
    }
}
