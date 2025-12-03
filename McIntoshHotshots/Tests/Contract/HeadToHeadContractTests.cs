using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;

namespace McIntoshHotshots.Tests.Contract;

/// <summary>
/// Contract tests for GET /api/stats/headtohead/{player1Id}/{player2Id} endpoint
/// These tests validate the API contract matches the OpenAPI specification
/// TDD: These tests should FAIL until the controller is implemented
/// </summary>
[TestClass]
public class HeadToHeadContractTests : ContractTestBase
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
    public async Task GetHeadToHead_ValidPlayerIds_Returns200WithHeadToHeadResponse()
    {
        // Arrange
        int player1Id = 1;
        int player2Id = 2;

        // Act
        var response = await Client!.GetAsync($"/api/stats/headtohead/{player1Id}/{player2Id}");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("application/json", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        // Validate HeadToHeadResponse contract
        Assert.IsTrue(root.TryGetProperty("player1", out _), "Response should contain 'player1'");
        Assert.IsTrue(root.TryGetProperty("player2", out _), "Response should contain 'player2'");
        Assert.IsTrue(root.TryGetProperty("overallRecord", out _), "Response should contain 'overallRecord'");
        Assert.IsTrue(root.TryGetProperty("recentForm", out _), "Response should contain 'recentForm'");
        Assert.IsTrue(root.TryGetProperty("tournamentBreakdown", out var breakdown), "Response should contain 'tournamentBreakdown'");
        Assert.AreEqual(JsonValueKind.Array, breakdown.ValueKind, "tournamentBreakdown should be an array");
        Assert.IsTrue(root.TryGetProperty("averageScoreDifferential", out _), "Response should contain 'averageScoreDifferential'");
        Assert.IsTrue(root.TryGetProperty("totalMatches", out _), "Response should contain 'totalMatches'");
        Assert.IsTrue(root.TryGetProperty("firstMatch", out _), "Response should contain 'firstMatch'");
        Assert.IsTrue(root.TryGetProperty("lastMatch", out _), "Response should contain 'lastMatch'");
    }

    [TestMethod]
    public async Task GetHeadToHead_PlayerSummaryStructure_MatchesContract()
    {
        // Arrange
        int player1Id = 1;
        int player2Id = 2;

        // Act
        var response = await Client!.GetAsync($"/api/stats/headtohead/{player1Id}/{player2Id}");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        // Validate PlayerSummary contract for player1
        Assert.IsTrue(root.TryGetProperty("player1", out var player1));
        Assert.IsTrue(player1.TryGetProperty("playerId", out _), "PlayerSummary should contain 'playerId'");
        Assert.IsTrue(player1.TryGetProperty("playerName", out _), "PlayerSummary should contain 'playerName'");
        Assert.IsTrue(player1.TryGetProperty("averageScore", out _), "PlayerSummary should contain 'averageScore'");
        Assert.IsTrue(player1.TryGetProperty("gamesPlayed", out _), "PlayerSummary should contain 'gamesPlayed'");

        // Validate PlayerSummary contract for player2
        Assert.IsTrue(root.TryGetProperty("player2", out var player2));
        Assert.IsTrue(player2.TryGetProperty("playerId", out _));
        Assert.IsTrue(player2.TryGetProperty("playerName", out _));
        Assert.IsTrue(player2.TryGetProperty("averageScore", out _));
        Assert.IsTrue(player2.TryGetProperty("gamesPlayed", out _));
    }

    [TestMethod]
    public async Task GetHeadToHead_OverallRecordStructure_MatchesContract()
    {
        // Arrange
        int player1Id = 1;
        int player2Id = 2;

        // Act
        var response = await Client!.GetAsync($"/api/stats/headtohead/{player1Id}/{player2Id}");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        // Validate OverallRecord contract
        Assert.IsTrue(root.TryGetProperty("overallRecord", out var record));
        Assert.IsTrue(record.TryGetProperty("player1Wins", out _), "OverallRecord should contain 'player1Wins'");
        Assert.IsTrue(record.TryGetProperty("player2Wins", out _), "OverallRecord should contain 'player2Wins'");
        Assert.IsTrue(record.TryGetProperty("winPercentagePlayer1", out _), "OverallRecord should contain 'winPercentagePlayer1'");
        Assert.IsTrue(record.TryGetProperty("winPercentagePlayer2", out _), "OverallRecord should contain 'winPercentagePlayer2'");
    }

    [TestMethod]
    public async Task GetHeadToHead_RecentFormStructure_MatchesContract()
    {
        // Arrange
        int player1Id = 1;
        int player2Id = 2;

        // Act
        var response = await Client!.GetAsync($"/api/stats/headtohead/{player1Id}/{player2Id}");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        // Validate RecentForm contract
        Assert.IsTrue(root.TryGetProperty("recentForm", out var recentForm));
        Assert.IsTrue(recentForm.TryGetProperty("recentMatches", out var matches), "RecentForm should contain 'recentMatches'");
        Assert.AreEqual(JsonValueKind.Array, matches.ValueKind, "recentMatches should be an array");
        Assert.IsTrue(recentForm.TryGetProperty("player1RecentWins", out _), "RecentForm should contain 'player1RecentWins'");
        Assert.IsTrue(recentForm.TryGetProperty("player2RecentWins", out _), "RecentForm should contain 'player2RecentWins'");

        // Validate RecentMatch structure if there are matches
        if (matches.GetArrayLength() > 0)
        {
            var firstMatch = matches[0];
            Assert.IsTrue(firstMatch.TryGetProperty("matchDate", out _), "RecentMatch should contain 'matchDate'");
            Assert.IsTrue(firstMatch.TryGetProperty("player1Score", out _), "RecentMatch should contain 'player1Score'");
            Assert.IsTrue(firstMatch.TryGetProperty("player2Score", out _), "RecentMatch should contain 'player2Score'");
            Assert.IsTrue(firstMatch.TryGetProperty("winnerId", out _), "RecentMatch should contain 'winnerId'");
            Assert.IsTrue(firstMatch.TryGetProperty("tournamentType", out _), "RecentMatch should contain 'tournamentType'");
        }
    }

    [TestMethod]
    public async Task GetHeadToHead_TournamentBreakdownStructure_MatchesContract()
    {
        // Arrange
        int player1Id = 1;
        int player2Id = 2;

        // Act
        var response = await Client!.GetAsync($"/api/stats/headtohead/{player1Id}/{player2Id}");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        Assert.IsTrue(root.TryGetProperty("tournamentBreakdown", out var breakdown));

        // Validate TournamentTypeRecord structure if there are records
        if (breakdown.GetArrayLength() > 0)
        {
            var firstRecord = breakdown[0];
            Assert.IsTrue(firstRecord.TryGetProperty("tournamentType", out _), "TournamentTypeRecord should contain 'tournamentType'");
            Assert.IsTrue(firstRecord.TryGetProperty("player1Wins", out _), "TournamentTypeRecord should contain 'player1Wins'");
            Assert.IsTrue(firstRecord.TryGetProperty("player2Wins", out _), "TournamentTypeRecord should contain 'player2Wins'");
            Assert.IsTrue(firstRecord.TryGetProperty("averageScoreDifferential", out _), "TournamentTypeRecord should contain 'averageScoreDifferential'");
        }
    }

    [TestMethod]
    public async Task GetHeadToHead_WithTournamentTypeFilter_Returns200()
    {
        // Arrange
        int player1Id = 1;
        int player2Id = 2;
        string tournamentType = "Singles";

        // Act
        var response = await Client!.GetAsync(
            $"/api/stats/headtohead/{player1Id}/{player2Id}?tournamentType={tournamentType}");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task GetHeadToHead_WithRecentGamesParameter_Returns200()
    {
        // Arrange
        int player1Id = 1;
        int player2Id = 2;
        int recentGames = 5;

        // Act
        var response = await Client!.GetAsync(
            $"/api/stats/headtohead/{player1Id}/{player2Id}?recentGames={recentGames}");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        // Verify that recentMatches respects the limit
        Assert.IsTrue(root.TryGetProperty("recentForm", out var recentForm));
        Assert.IsTrue(recentForm.TryGetProperty("recentMatches", out var matches));

        if (matches.GetArrayLength() > 0)
        {
            Assert.IsTrue(matches.GetArrayLength() <= recentGames,
                $"Should return at most {recentGames} recent matches");
        }
    }

    [TestMethod]
    public async Task GetHeadToHead_RecentGamesDefault_Is10()
    {
        // Arrange
        int player1Id = 1;
        int player2Id = 2;

        // Act - No recentGames parameter specified
        var response = await Client!.GetAsync($"/api/stats/headtohead/{player1Id}/{player2Id}");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        // Verify that default is 10 games
        Assert.IsTrue(root.TryGetProperty("recentForm", out var recentForm));
        Assert.IsTrue(recentForm.TryGetProperty("recentMatches", out var matches));

        if (matches.GetArrayLength() > 0)
        {
            Assert.IsTrue(matches.GetArrayLength() <= 10,
                "Should return at most 10 recent matches by default");
        }
    }

    [TestMethod]
    public async Task GetHeadToHead_RecentGamesBelowMinimum_Returns400BadRequest()
    {
        // Arrange
        int player1Id = 1;
        int player2Id = 2;
        int invalidRecentGames = 0; // Minimum is 1

        // Act
        var response = await Client!.GetAsync(
            $"/api/stats/headtohead/{player1Id}/{player2Id}?recentGames={invalidRecentGames}");

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task GetHeadToHead_RecentGamesAboveMaximum_Returns400BadRequest()
    {
        // Arrange
        int player1Id = 1;
        int player2Id = 2;
        int invalidRecentGames = 51; // Maximum is 50

        // Act
        var response = await Client!.GetAsync(
            $"/api/stats/headtohead/{player1Id}/{player2Id}?recentGames={invalidRecentGames}");

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task GetHeadToHead_SamePlayerIds_Returns400BadRequest()
    {
        // Arrange
        int playerId = 1;

        // Act
        var response = await Client!.GetAsync($"/api/stats/headtohead/{playerId}/{playerId}");

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        // Verify ErrorResponse contract
        Assert.IsTrue(root.TryGetProperty("error", out _), "Error response should contain 'error'");
        Assert.IsTrue(root.TryGetProperty("message", out var message), "Error response should contain 'message'");

        // Error message should indicate that player IDs cannot be the same
        var messageText = message.GetString();
        Assert.IsTrue(messageText?.Contains("same") == true || messageText?.Contains("cannot") == true,
            "Error message should indicate player IDs cannot be the same");
    }

    [TestMethod]
    public async Task GetHeadToHead_NonExistentPlayer1_Returns404NotFound()
    {
        // Arrange
        int nonExistentPlayer1Id = 999999;
        int validPlayer2Id = 2;

        // Act
        var response = await Client!.GetAsync(
            $"/api/stats/headtohead/{nonExistentPlayer1Id}/{validPlayer2Id}");

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
    public async Task GetHeadToHead_NonExistentPlayer2_Returns404NotFound()
    {
        // Arrange
        int validPlayer1Id = 1;
        int nonExistentPlayer2Id = 999999;

        // Act
        var response = await Client!.GetAsync(
            $"/api/stats/headtohead/{validPlayer1Id}/{nonExistentPlayer2Id}");

        // Assert
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task GetHeadToHead_BothPlayersNonExistent_Returns404NotFound()
    {
        // Arrange
        int nonExistentPlayer1Id = 999999;
        int nonExistentPlayer2Id = 888888;

        // Act
        var response = await Client!.GetAsync(
            $"/api/stats/headtohead/{nonExistentPlayer1Id}/{nonExistentPlayer2Id}");

        // Assert
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task GetHeadToHead_WithAllParameters_Returns200()
    {
        // Arrange
        int player1Id = 1;
        int player2Id = 2;
        string tournamentType = "Doubles";
        int recentGames = 20;

        // Act
        var response = await Client!.GetAsync(
            $"/api/stats/headtohead/{player1Id}/{player2Id}?tournamentType={tournamentType}&recentGames={recentGames}");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }
}
