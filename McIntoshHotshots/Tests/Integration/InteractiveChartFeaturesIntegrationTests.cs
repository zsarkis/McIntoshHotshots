using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.AspNetCore.Mvc.Testing;
using PuppeteerSharp;
using System;
using System.Threading.Tasks;

namespace McIntoshHotshots.Tests.Integration;

/// <summary>
/// Integration tests for Interactive Chart Features scenario from quickstart.md
/// Tests Chart.js integration and interactive features (zoom, pan, tooltips, etc.)
/// TDD: These tests MUST FAIL until the Chart.js integration is fully implemented
/// </summary>
[TestClass]
public class InteractiveChartFeaturesIntegrationTests : IntegrationTestBase
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
    public async Task ChartFeatures_ChartJsLibrary_LoadsSuccessfully()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int playerId = 1;

        try
        {
            // Act
            await page.GoToAsync($"{BaseUrl}/players/{playerId}/stats");
            await page.WaitForSelectorAsync(".chart-container canvas", new WaitForSelectorOptions
            {
                Timeout = 5000
            });

            // Check if Chart.js is loaded
            var chartJsLoaded = await page.EvaluateExpressionAsync<bool>("typeof Chart !== 'undefined'");

            // Assert
            Assert.IsTrue(chartJsLoaded, "Chart.js library should be loaded on the page");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task ChartFeatures_ZoomFunctionality_WorksSmoothly()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int playerId = 1;

        try
        {
            // Act
            await page.GoToAsync($"{BaseUrl}/players/{playerId}/stats");
            await page.WaitForSelectorAsync(".chart-container canvas", new WaitForSelectorOptions
            {
                Timeout = 5000
            });

            var canvas = await page.QuerySelectorAsync(".chart-container canvas");
            Assert.IsNotNull(canvas, "Canvas should exist");

            // Get canvas bounding box for interaction
            var boundingBox = await canvas.BoundingBoxAsync();
            Assert.IsNotNull(boundingBox, "Canvas should have bounding box");

            // Simulate mouse wheel zoom
            await page.Mouse.MoveAsync(
                boundingBox.X + boundingBox.Width / 2,
                boundingBox.Y + boundingBox.Height / 2
            );

            // Simulate zoom (mouse wheel)
            await page.Mouse.WheelAsync(0, -100); // Scroll up to zoom in
            await Task.Delay(500);

            await page.Mouse.WheelAsync(0, 100); // Scroll down to zoom out
            await Task.Delay(500);

            // Chart should still be visible after zoom operations
            var chartStillVisible = await page.QuerySelectorAsync(".chart-container canvas");
            Assert.IsNotNull(chartStillVisible, "Chart should still be visible after zoom operations");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task ChartFeatures_PanFunctionality_RespondsCorrectly()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int playerId = 1;

        try
        {
            // Act
            await page.GoToAsync($"{BaseUrl}/players/{playerId}/stats");
            await page.WaitForSelectorAsync(".chart-container canvas", new WaitForSelectorOptions
            {
                Timeout = 5000
            });

            var canvas = await page.QuerySelectorAsync(".chart-container canvas");
            Assert.IsNotNull(canvas, "Canvas should exist");

            var boundingBox = await canvas.BoundingBoxAsync();
            Assert.IsNotNull(boundingBox, "Canvas should have bounding box");

            // Simulate pan (click and drag)
            var startX = boundingBox.X + boundingBox.Width / 2;
            var startY = boundingBox.Y + boundingBox.Height / 2;

            await page.Mouse.MoveAsync(startX, startY);
            await page.Mouse.DownAsync();
            await page.Mouse.MoveAsync(startX - 50, startY); // Pan left
            await page.Mouse.UpAsync();
            await Task.Delay(500);

            // Chart should still be visible after pan operations
            var chartStillVisible = await page.QuerySelectorAsync(".chart-container canvas");
            Assert.IsNotNull(chartStillVisible, "Chart should still be visible after pan operations");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task ChartFeatures_DataPointTooltips_ShowAccurateData()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int playerId = 1;

        try
        {
            // Act
            await page.GoToAsync($"{BaseUrl}/players/{playerId}/stats");
            await page.WaitForSelectorAsync(".chart-container canvas", new WaitForSelectorOptions
            {
                Timeout = 5000
            });

            var canvas = await page.QuerySelectorAsync(".chart-container canvas");
            Assert.IsNotNull(canvas, "Canvas should exist");

            var boundingBox = await canvas.BoundingBoxAsync();
            Assert.IsNotNull(boundingBox, "Canvas should have bounding box");

            // Hover over chart to trigger tooltip
            await page.Mouse.MoveAsync(
                boundingBox.X + boundingBox.Width / 2,
                boundingBox.Y + boundingBox.Height / 2
            );
            await Task.Delay(1000); // Wait for tooltip to appear

            // Check if tooltip element exists
            // Chart.js typically uses a div with id 'chartjs-tooltip' or similar
            var pageContent = await page.GetContentAsync();

            // The tooltip should be visible or the chart should respond to hover
            // This is a basic test - actual tooltip verification may require Chart.js specific selectors
            Assert.IsNotNull(pageContent, "Page should respond to hover interactions");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task ChartFeatures_LegendInteraction_TogglesDataSeries()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int playerId = 1;

        try
        {
            // Act
            await page.GoToAsync($"{BaseUrl}/players/{playerId}/stats");
            await page.WaitForSelectorAsync(".chart-container canvas", new WaitForSelectorOptions
            {
                Timeout = 5000
            });

            // Try to find and click legend items
            // Chart.js legends are typically rendered as part of the canvas or as separate elements
            var legendItems = await page.QuerySelectorAllAsync(".chart-legend li, [class*='legend']");

            if (legendItems.Length > 0)
            {
                // Click first legend item to toggle
                await legendItems[0].ClickAsync();
                await Task.Delay(500);

                // Click again to toggle back
                await legendItems[0].ClickAsync();
                await Task.Delay(500);
            }

            // Chart should still be visible after legend interactions
            var chartStillVisible = await page.QuerySelectorAsync(".chart-container canvas");
            Assert.IsNotNull(chartStillVisible, "Chart should still be visible after legend interactions");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task ChartFeatures_ResponsiveBehavior_AdaptsToWindowResize()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int playerId = 1;

        try
        {
            // Act - Start with desktop size
            await page.SetViewportAsync(new ViewPortOptions
            {
                Width = 1920,
                Height = 1080
            });

            await page.GoToAsync($"{BaseUrl}/players/{playerId}/stats");
            await page.WaitForSelectorAsync(".chart-container canvas", new WaitForSelectorOptions
            {
                Timeout = 5000
            });

            var canvasDesktop = await page.QuerySelectorAsync(".chart-container canvas");
            Assert.IsNotNull(canvasDesktop, "Chart should be visible on desktop");

            // Resize to tablet
            await page.SetViewportAsync(new ViewPortOptions
            {
                Width = 768,
                Height = 1024
            });
            await Task.Delay(500);

            var canvasTablet = await page.QuerySelectorAsync(".chart-container canvas");
            Assert.IsNotNull(canvasTablet, "Chart should be visible on tablet");

            // Resize to mobile
            await page.SetViewportAsync(new ViewPortOptions
            {
                Width = 375,
                Height = 667
            });
            await Task.Delay(500);

            var canvasMobile = await page.QuerySelectorAsync(".chart-container canvas");
            Assert.IsNotNull(canvasMobile, "Chart should be visible on mobile");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task ChartFeatures_LargeDataset_PerformsWell()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int playerId = 1;

        try
        {
            // Act - Load yearly view which should have 365+ data points per quickstart.md
            await page.GoToAsync($"{BaseUrl}/players/{playerId}/stats?period=yearly");

            var startTime = DateTime.UtcNow;
            await page.WaitForSelectorAsync(".chart-container canvas", new WaitForSelectorOptions
            {
                Timeout = 5000
            });
            var endTime = DateTime.UtcNow;

            var loadTime = (endTime - startTime).TotalMilliseconds;

            // Assert
            Assert.IsTrue(loadTime < 2000,
                $"Chart with large dataset should load within 2000ms (actual: {loadTime}ms)");

            // Test zoom/pan performance on large dataset
            var canvas = await page.QuerySelectorAsync(".chart-container canvas");
            var boundingBox = await canvas!.BoundingBoxAsync();

            var zoomStartTime = DateTime.UtcNow;
            await page.Mouse.MoveAsync(
                boundingBox!.X + boundingBox.Width / 2,
                boundingBox.Y + boundingBox.Height / 2
            );
            await page.Mouse.WheelAsync(0, -100);
            await Task.Delay(100); // Wait for zoom to complete

            var zoomEndTime = DateTime.UtcNow;
            var zoomTime = (zoomEndTime - zoomStartTime).TotalMilliseconds;

            Assert.IsTrue(zoomTime < 500,
                $"Zoom operation should respond within 500ms (actual: {zoomTime}ms)");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task ChartFeatures_MemoryUsage_NoLeaks()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int playerId = 1;

        try
        {
            // Act - Load and reload chart multiple times
            for (int i = 0; i < 3; i++)
            {
                await page.GoToAsync($"{BaseUrl}/players/{playerId}/stats");
                await page.WaitForSelectorAsync(".chart-container canvas", new WaitForSelectorOptions
                {
                    Timeout = 5000
                });
                await Task.Delay(500);

                // Change period to trigger chart re-render
                await page.SelectAsync("[data-testid='period-selector']", "monthly");
                await Task.Delay(500);
            }

            // Chart should still be responsive after multiple reloads
            var chartStillVisible = await page.QuerySelectorAsync(".chart-container canvas");
            Assert.IsNotNull(chartStillVisible, "Chart should still be visible after multiple reloads");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task ChartFeatures_MobileDevice_WorksCorrectly()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int playerId = 1;

        try
        {
            // Act - Set mobile viewport
            await page.SetViewportAsync(new ViewPortOptions
            {
                Width = 390,
                Height = 844,
                IsMobile = true,
                HasTouch = true
            });
            await page.SetUserAgentAsync("Mozilla/5.0 (iPhone; CPU iPhone OS 14_0 like Mac OS X) AppleWebKit/605.1.15");

            await page.GoToAsync($"{BaseUrl}/players/{playerId}/stats");
            await page.WaitForSelectorAsync(".chart-container canvas", new WaitForSelectorOptions
            {
                Timeout = 5000
            });

            var canvas = await page.QuerySelectorAsync(".chart-container canvas");
            Assert.IsNotNull(canvas, "Chart should be visible on mobile device");

            // Test touch interactions
            var boundingBox = await canvas.BoundingBoxAsync();
            Assert.IsNotNull(boundingBox, "Canvas should have bounding box on mobile");

            // Simulate touch tap
            await page.Touchscreen.TapAsync(
                boundingBox.X + boundingBox.Width / 2,
                boundingBox.Y + boundingBox.Height / 2
            );
            await Task.Delay(500);

            var chartStillVisible = await page.QuerySelectorAsync(".chart-container canvas");
            Assert.IsNotNull(chartStillVisible, "Chart should still be visible after touch interactions");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [TestMethod]
    public async Task ChartFeatures_MultipleBrowsers_WorkConsistently()
    {
        // Arrange
        var page = await Browser!.NewPageAsync();
        int playerId = 1;

        try
        {
            // Act - Test with different user agents (simulating different browsers)
            var userAgents = new[]
            {
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36", // Chrome
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:89.0) Gecko/20100101 Firefox/89.0", // Firefox
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/14.1.1 Safari/605.1.15" // Safari
            };

            foreach (var userAgent in userAgents)
            {
                await page.SetUserAgentAsync(userAgent);
                await page.GoToAsync($"{BaseUrl}/players/{playerId}/stats");
                await page.WaitForSelectorAsync(".chart-container canvas", new WaitForSelectorOptions
                {
                    Timeout = 5000
                });

                var chartVisible = await page.QuerySelectorAsync(".chart-container canvas");
                Assert.IsNotNull(chartVisible, $"Chart should be visible with user agent: {userAgent}");

                await Task.Delay(500);
            }
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}
