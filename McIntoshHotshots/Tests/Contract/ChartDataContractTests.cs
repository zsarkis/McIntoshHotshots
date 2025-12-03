using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;

namespace McIntoshHotshots.Tests.Contract;

/// <summary>
/// Contract tests for POST /api/stats/chart-data endpoint
/// These tests validate the API contract matches the OpenAPI specification
/// TDD: These tests should FAIL until the controller is implemented
/// </summary>
[TestClass]
public class ChartDataContractTests : ContractTestBase
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

    private StringContent CreateJsonContent(object data)
    {
        var json = JsonSerializer.Serialize(data);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    [TestMethod]
    public async Task PostChartData_ValidTimeSeriesRequest_Returns200WithChartDataResponse()
    {
        // Arrange
        var request = new
        {
            chartType = "timeseries",
            players = new[] { 1, 2 },
            metrics = new[] { "average", "median" },
            period = "monthly",
            dateRange = new
            {
                startDate = "2024-01-01",
                endDate = "2024-12-31"
            }
        };

        // Act
        var response = await Client!.PostAsync("/api/stats/chart-data", CreateJsonContent(request));

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("application/json", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        // Validate ChartDataResponse contract
        Assert.IsTrue(root.TryGetProperty("chartType", out _), "Response should contain 'chartType'");
        Assert.IsTrue(root.TryGetProperty("datasets", out var datasets), "Response should contain 'datasets'");
        Assert.AreEqual(JsonValueKind.Array, datasets.ValueKind, "datasets should be an array");
        Assert.IsTrue(root.TryGetProperty("labels", out var labels), "Response should contain 'labels'");
        Assert.AreEqual(JsonValueKind.Array, labels.ValueKind, "labels should be an array");
        Assert.IsTrue(root.TryGetProperty("options", out _), "Response should contain 'options'");
        Assert.IsTrue(root.TryGetProperty("metadata", out _), "Response should contain 'metadata'");
    }

    [TestMethod]
    public async Task PostChartData_ValidHeadToHeadRequest_Returns200()
    {
        // Arrange
        var request = new
        {
            chartType = "headtohead",
            players = new[] { 1, 2 },
            metrics = new[] { "average", "checkout_percentage" },
            period = "weekly"
        };

        // Act
        var response = await Client!.PostAsync("/api/stats/chart-data", CreateJsonContent(request));

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task PostChartData_ValidComparisonRequest_Returns200()
    {
        // Arrange
        var request = new
        {
            chartType = "comparison",
            players = new[] { 1, 2, 3 },
            tournaments = new[] { 10, 20 },
            metrics = new[] { "completion_rate", "doubles_hit_rate" },
            period = "quarterly"
        };

        // Act
        var response = await Client!.PostAsync("/api/stats/chart-data", CreateJsonContent(request));

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task PostChartData_DatasetsStructure_MatchesContract()
    {
        // Arrange
        var request = new
        {
            chartType = "timeseries",
            players = new[] { 1 },
            metrics = new[] { "average" },
            period = "monthly"
        };

        // Act
        var response = await Client!.PostAsync("/api/stats/chart-data", CreateJsonContent(request));

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        Assert.IsTrue(root.TryGetProperty("datasets", out var datasets));

        // If there are datasets, validate ChartDataset structure
        if (datasets.GetArrayLength() > 0)
        {
            var firstDataset = datasets[0];

            // Validate ChartDataset contract
            Assert.IsTrue(firstDataset.TryGetProperty("label", out _), "ChartDataset should contain 'label'");
            Assert.IsTrue(firstDataset.TryGetProperty("data", out var data), "ChartDataset should contain 'data'");
            Assert.AreEqual(JsonValueKind.Array, data.ValueKind, "ChartDataset.data should be an array");
            Assert.IsTrue(firstDataset.TryGetProperty("backgroundColor", out _), "ChartDataset should contain 'backgroundColor'");
            Assert.IsTrue(firstDataset.TryGetProperty("borderColor", out _), "ChartDataset should contain 'borderColor'");
            Assert.IsTrue(firstDataset.TryGetProperty("tension", out _), "ChartDataset should contain 'tension'");
        }
    }

    [TestMethod]
    public async Task PostChartData_MetadataStructure_MatchesContract()
    {
        // Arrange
        var request = new
        {
            chartType = "timeseries",
            players = new[] { 1 },
            metrics = new[] { "average" },
            period = "monthly"
        };

        // Act
        var response = await Client!.PostAsync("/api/stats/chart-data", CreateJsonContent(request));

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        Assert.IsTrue(root.TryGetProperty("metadata", out var metadata));

        // Validate ChartMetadata contract
        Assert.IsTrue(metadata.TryGetProperty("totalDataPoints", out _), "ChartMetadata should contain 'totalDataPoints'");
        Assert.IsTrue(metadata.TryGetProperty("hasInsufficientData", out _), "ChartMetadata should contain 'hasInsufficientData'");
        Assert.IsTrue(metadata.TryGetProperty("recommendedChartType", out _), "ChartMetadata should contain 'recommendedChartType'");
        Assert.IsTrue(metadata.TryGetProperty("performance", out var performance), "ChartMetadata should contain 'performance'");

        // Validate PerformanceMetadata contract
        Assert.IsTrue(performance.TryGetProperty("queryTimeMs", out _), "PerformanceMetadata should contain 'queryTimeMs'");
        Assert.IsTrue(performance.TryGetProperty("cacheHit", out _), "PerformanceMetadata should contain 'cacheHit'");
        Assert.IsTrue(performance.TryGetProperty("aggregationTimeMs", out _), "PerformanceMetadata should contain 'aggregationTimeMs'");
    }

    [TestMethod]
    public async Task PostChartData_AllValidChartTypes_Return200()
    {
        // Arrange - Test all valid chart types from OpenAPI spec
        string[] validChartTypes = { "timeseries", "headtohead", "comparison" };

        foreach (var chartType in validChartTypes)
        {
            var request = new
            {
                chartType = chartType,
                players = new[] { 1 },
                metrics = new[] { "average" },
                period = "monthly"
            };

            // Act
            var response = await Client!.PostAsync("/api/stats/chart-data", CreateJsonContent(request));

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
                $"ChartType '{chartType}' should return 200 OK");

            var content = await response.Content.ReadAsStringAsync();
            var jsonDocument = JsonDocument.Parse(content);
            var root = jsonDocument.RootElement;

            Assert.IsTrue(root.TryGetProperty("chartType", out var returnedType));
            Assert.AreEqual(chartType, returnedType.GetString());
        }
    }

    [TestMethod]
    public async Task PostChartData_AllValidPeriods_Return200()
    {
        // Arrange - Test all valid periods from OpenAPI spec
        string[] validPeriods = { "weekly", "monthly", "quarterly", "yearly" };

        foreach (var period in validPeriods)
        {
            var request = new
            {
                chartType = "timeseries",
                players = new[] { 1 },
                metrics = new[] { "average" },
                period = period
            };

            // Act
            var response = await Client!.PostAsync("/api/stats/chart-data", CreateJsonContent(request));

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
                $"Period '{period}' should return 200 OK");
        }
    }

    [TestMethod]
    public async Task PostChartData_InvalidChartType_Returns400BadRequest()
    {
        // Arrange
        var request = new
        {
            chartType = "invalidType",
            players = new[] { 1 },
            metrics = new[] { "average" },
            period = "monthly"
        };

        // Act
        var response = await Client!.PostAsync("/api/stats/chart-data", CreateJsonContent(request));

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
    public async Task PostChartData_InvalidPeriod_Returns400BadRequest()
    {
        // Arrange
        var request = new
        {
            chartType = "timeseries",
            players = new[] { 1 },
            metrics = new[] { "average" },
            period = "invalidPeriod"
        };

        // Act
        var response = await Client!.PostAsync("/api/stats/chart-data", CreateJsonContent(request));

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task PostChartData_MissingRequiredField_Returns400BadRequest()
    {
        // Arrange - Missing 'chartType' field
        var request = new
        {
            players = new[] { 1 },
            metrics = new[] { "average" },
            period = "monthly"
        };

        // Act
        var response = await Client!.PostAsync("/api/stats/chart-data", CreateJsonContent(request));

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task PostChartData_EmptyPlayersArray_Returns400BadRequest()
    {
        // Arrange
        var request = new
        {
            chartType = "timeseries",
            players = Array.Empty<int>(),
            metrics = new[] { "average" },
            period = "monthly"
        };

        // Act
        var response = await Client!.PostAsync("/api/stats/chart-data", CreateJsonContent(request));

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task PostChartData_EmptyMetricsArray_Returns400BadRequest()
    {
        // Arrange
        var request = new
        {
            chartType = "timeseries",
            players = new[] { 1 },
            metrics = Array.Empty<string>(),
            period = "monthly"
        };

        // Act
        var response = await Client!.PostAsync("/api/stats/chart-data", CreateJsonContent(request));

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task PostChartData_WithDateRange_Returns200()
    {
        // Arrange
        var request = new
        {
            chartType = "timeseries",
            players = new[] { 1 },
            metrics = new[] { "average" },
            period = "monthly",
            dateRange = new
            {
                startDate = "2024-01-01",
                endDate = "2024-12-31"
            }
        };

        // Act
        var response = await Client!.PostAsync("/api/stats/chart-data", CreateJsonContent(request));

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task PostChartData_WithTournaments_Returns200()
    {
        // Arrange
        var request = new
        {
            chartType = "comparison",
            players = new[] { 1, 2 },
            tournaments = new[] { 10, 20, 30 },
            metrics = new[] { "average", "median" },
            period = "monthly"
        };

        // Act
        var response = await Client!.PostAsync("/api/stats/chart-data", CreateJsonContent(request));

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task PostChartData_InvalidDateFormat_Returns400BadRequest()
    {
        // Arrange
        var request = new
        {
            chartType = "timeseries",
            players = new[] { 1 },
            metrics = new[] { "average" },
            period = "monthly",
            dateRange = new
            {
                startDate = "not-a-date",
                endDate = "2024-12-31"
            }
        };

        // Act
        var response = await Client!.PostAsync("/api/stats/chart-data", CreateJsonContent(request));

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task PostChartData_InvalidJson_Returns400BadRequest()
    {
        // Arrange
        var invalidJson = new StringContent("{invalid json}", Encoding.UTF8, "application/json");

        // Act
        var response = await Client!.PostAsync("/api/stats/chart-data", invalidJson);

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task PostChartData_EmptyBody_Returns400BadRequest()
    {
        // Arrange
        var emptyContent = new StringContent("", Encoding.UTF8, "application/json");

        // Act
        var response = await Client!.PostAsync("/api/stats/chart-data", emptyContent);

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task PostChartData_LabelsArray_ContainsStrings()
    {
        // Arrange
        var request = new
        {
            chartType = "timeseries",
            players = new[] { 1 },
            metrics = new[] { "average" },
            period = "monthly"
        };

        // Act
        var response = await Client!.PostAsync("/api/stats/chart-data", CreateJsonContent(request));

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        Assert.IsTrue(root.TryGetProperty("labels", out var labels));

        // If there are labels, verify they are strings
        if (labels.GetArrayLength() > 0)
        {
            var firstLabel = labels[0];
            Assert.AreEqual(JsonValueKind.String, firstLabel.ValueKind, "Labels should be strings");
        }
    }

    [TestMethod]
    public async Task PostChartData_DataArray_ContainsNumbers()
    {
        // Arrange
        var request = new
        {
            chartType = "timeseries",
            players = new[] { 1 },
            metrics = new[] { "average" },
            period = "monthly"
        };

        // Act
        var response = await Client!.PostAsync("/api/stats/chart-data", CreateJsonContent(request));

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        Assert.IsTrue(root.TryGetProperty("datasets", out var datasets));

        // If there are datasets with data, verify data points are numbers
        if (datasets.GetArrayLength() > 0)
        {
            var firstDataset = datasets[0];
            Assert.IsTrue(firstDataset.TryGetProperty("data", out var data));

            if (data.GetArrayLength() > 0)
            {
                var firstDataPoint = data[0];
                Assert.AreEqual(JsonValueKind.Number, firstDataPoint.ValueKind, "Data points should be numbers");
            }
        }
    }
}
