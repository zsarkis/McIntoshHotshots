using Microsoft.VisualStudio.TestTools.UnitTesting;
using McIntoshHotshots.Services;
using System.Linq;

namespace McIntoshHotshots.Tests.Services;

[TestClass]
public class DartsRuleEngineTests
{
    [TestMethod]
    public void Constructor_WithValidPlayerIds_InitializesGameCorrectly()
    {
        // Arrange & Act
        var engine = new DartsRuleEngine("Player1", "Player2");

        // Assert
        Assert.IsFalse(engine.IsGameOver());
        Assert.IsNull(engine.GetWinner());
        Assert.AreEqual("Player1", engine.GetCurrentPlayer().Id);
        Assert.AreEqual(501, engine.GetRemainingScore("Player1"));
        Assert.AreEqual(501, engine.GetRemainingScore("Player2"));
        Assert.AreEqual(0, engine.GetDartsThrown());
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Constructor_WithNullPlayerId_ThrowsException()
    {
        new DartsRuleEngine(null, "Player2");
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Constructor_WithSamePlayerIds_ThrowsException()
    {
        new DartsRuleEngine("Player1", "Player1");
    }

    [TestMethod]
    public void RecordThrow_SimpleScoring_UpdatesScoreCorrectly()
    {
        // Arrange
        var engine = new DartsRuleEngine("Player1", "Player2");

        // Act
        var result = engine.RecordThrow("Player1", "20", "single");

        // Assert
        Assert.IsTrue(result.IsValid);
        Assert.IsFalse(result.IsBust);
        Assert.IsFalse(result.IsFinish);
        Assert.AreEqual(481, result.NewScore); // 501 - 20 = 481
        Assert.AreEqual(20, result.DartPoints);
        Assert.AreEqual(1, result.DartsThrown);
        Assert.AreEqual("Player1", result.CurrentPlayerId);
    }

    [TestMethod]
    public void RecordThrow_DoubleAndTrebleScoring_CalculatesCorrectly()
    {
        // Arrange
        var engine = new DartsRuleEngine("Player1", "Player2");

        // Act - Double 20
        var result1 = engine.RecordThrow("Player1", "20", "double");
        // Act - Treble 19
        var result2 = engine.RecordThrow("Player1", "19", "treble");

        // Assert
        Assert.AreEqual(461, result1.NewScore); // 501 - 40 = 461
        Assert.AreEqual(40, result1.DartPoints);
        
        Assert.AreEqual(404, result2.NewScore); // 461 - 57 = 404
        Assert.AreEqual(57, result2.DartPoints);
    }

    [TestMethod]
    public void RecordThrow_BullSegments_CalculatesCorrectly()
    {
        // Arrange
        var engine = new DartsRuleEngine("Player1", "Player2");

        // Act - Outer bull (single)
        var result1 = engine.RecordThrow("Player1", "bull", "single");
        // Act - Bullseye (double)
        var result2 = engine.RecordThrow("Player1", "bull", "double");

        // Assert
        Assert.AreEqual(476, result1.NewScore); // 501 - 25 = 476
        Assert.AreEqual(25, result1.DartPoints);
        
        Assert.AreEqual(426, result2.NewScore); // 476 - 50 = 426
        Assert.AreEqual(50, result2.DartPoints);
    }

    [TestMethod]
    public void RecordThrow_InvalidSegment_ReturnsError()
    {
        // Arrange
        var engine = new DartsRuleEngine("Player1", "Player2");

        // Act
        var result = engine.RecordThrow("Player1", "21", "single");

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("Invalid segment or ring type combination", result.ErrorMessage);
    }

    [TestMethod]
    public void RecordThrow_InvalidRingType_ReturnsError()
    {
        // Arrange
        var engine = new DartsRuleEngine("Player1", "Player2");

        // Act
        var result = engine.RecordThrow("Player1", "20", "quadruple");

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("Invalid segment or ring type combination", result.ErrorMessage);
    }

    [TestMethod]
    public void RecordThrow_WrongPlayerTurn_ReturnsError()
    {
        // Arrange
        var engine = new DartsRuleEngine("Player1", "Player2");

        // Act
        var result = engine.RecordThrow("Player2", "20", "single");

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("Not Player2's turn. Current player: Player1", result.ErrorMessage);
    }

    [TestMethod]
    public void RecordThrow_BustGoingBelowZero_HandlesCorrectly()
    {
        // Arrange
        var engine = new DartsRuleEngine("Player1", "Player2");
        
        // Manually set player to a low score by adjusting their score directly
        var player1 = engine.GetPlayers().First(p => p.Id == "Player1");
        player1.Score = 30; // Set to exactly 30 points
        
        // Act - Try to score more than remaining (60 points when only 30 left)
        var result = engine.RecordThrow("Player1", "20", "treble"); // 60 points

        // Assert
        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(result.IsBust);
        Assert.AreEqual("Score would go below zero", result.BustReason);
        Assert.AreEqual(30, result.NewScore); // Score should be reset to start of turn
        Assert.AreEqual("Player2", engine.GetCurrentPlayer().Id); // Turn advanced
    }

    [TestMethod]
    public void RecordThrow_BustEndingOnOne_HandlesCorrectly()
    {
        // Arrange
        var engine = new DartsRuleEngine("Player1", "Player2");
        
        // Directly set player to exactly 2 points so next dart leaves 1
        var player1 = engine.GetPlayers().First(p => p.Id == "Player1");
        player1.Score = 2;

        // Act - Try to leave exactly 1 point
        var result = engine.RecordThrow("Player1", "1", "single");

        // Assert
        Assert.IsTrue(result.IsBust);
        Assert.AreEqual("Cannot finish on 1 (no double 0.5)", result.BustReason);
        Assert.AreEqual(2, result.NewScore); // Score should be reset to start of turn
    }

    [TestMethod]
    public void RecordThrow_ValidDoubleFinish_WinsGame()
    {
        // Arrange
        var engine = new DartsRuleEngine("Player1", "Player2");
        
        // Manually set player to exactly 40 points for a clean double 20 finish
        var player1 = engine.GetPlayers().First(p => p.Id == "Player1");
        player1.Score = 40;

        // Act - Finish with double 20
        var result = engine.RecordThrow("Player1", "20", "double");

        // Assert
        Assert.IsTrue(result.IsValid);
        Assert.IsFalse(result.IsBust);
        Assert.IsTrue(result.IsFinish);
        Assert.IsTrue(result.IsGameOver);
        Assert.AreEqual("Player1", result.WinnerId);
        Assert.AreEqual(0, result.NewScore);
        Assert.IsTrue(engine.IsGameOver());
        Assert.AreEqual("Player1", engine.GetWinner());
    }

    [TestMethod]
    public void RecordThrow_InvalidFinishNotDouble_Busts()
    {
        // Arrange
        var engine = new DartsRuleEngine("Player1", "Player2");
        
        // Directly set player to exactly 20 points
        var player1 = engine.GetPlayers().First(p => p.Id == "Player1");
        player1.Score = 20;
        
        // Act - Try to finish with single 20 (should bust)
        var result = engine.RecordThrow("Player1", "20", "single");

        // Assert
        Assert.IsTrue(result.IsBust);
        Assert.AreEqual("Must finish with a double", result.BustReason);
        Assert.AreEqual(20, result.NewScore); // Score reset to start of turn
    }

    [TestMethod]
    public void TurnManagement_ThreeDartsAdvancesTurn()
    {
        // Arrange
        var engine = new DartsRuleEngine("Player1", "Player2");

        // Act - Throw 3 darts
        engine.RecordThrow("Player1", "1", "single");
        engine.RecordThrow("Player1", "1", "single");
        var result = engine.RecordThrow("Player1", "1", "single");

        // Assert
        Assert.AreEqual(0, engine.GetDartsThrown()); // Reset to 0
        Assert.AreEqual("Player2", engine.GetCurrentPlayer().Id); // Advanced to next player
    }

    [TestMethod]
    public void TurnManagement_BustAdvancesTurn()
    {
        // Arrange
        var engine = new DartsRuleEngine("Player1", "Player2");
        
        // Set up a guaranteed bust scenario - player has 30 points, throw 60
        var player1 = engine.GetPlayers().First(p => p.Id == "Player1");
        player1.Score = 30;

        // Act - Throw a dart that will definitely bust (60 > 30)
        var result = engine.RecordThrow("Player1", "20", "treble"); // 60 points

        // Assert
        Assert.IsTrue(result.IsBust);
        Assert.AreEqual(0, engine.GetDartsThrown()); // Turn ended
        Assert.AreEqual("Player2", engine.GetCurrentPlayer().Id); // Turn advanced
    }

    [TestMethod]
    public void NextTurn_ManuallyAdvancesTurn()
    {
        // Arrange
        var engine = new DartsRuleEngine("Player1", "Player2");
        engine.RecordThrow("Player1", "20", "single"); // Throw one dart

        // Act
        engine.NextTurn();

        // Assert
        Assert.AreEqual("Player2", engine.GetCurrentPlayer().Id);
        Assert.AreEqual(0, engine.GetDartsThrown());
    }

    [TestMethod]
    public void GetCheckoutRecommendations_ValidScore_ReturnsRecommendations()
    {
        // Arrange
        var engine = new DartsRuleEngine("Player1", "Player2");

        // Act
        var recommendations = engine.GetCheckoutRecommendations(100);

        // Assert
        Assert.IsTrue(recommendations.Count > 0);
        Assert.IsTrue(recommendations.Any(r => r.Darts == 2)); // Should have 2-dart finish
    }

    [TestMethod]
    public void GetCheckoutRecommendations_InvalidScore_ReturnsEmpty()
    {
        // Arrange
        var engine = new DartsRuleEngine("Player1", "Player2");

        // Act
        var recommendations1 = engine.GetCheckoutRecommendations(1);
        var recommendations2 = engine.GetCheckoutRecommendations(171);

        // Assert
        Assert.AreEqual(0, recommendations1.Count);
        Assert.AreEqual(0, recommendations2.Count);
    }

    [TestMethod]
    public void GameAfterFinish_RejectsNewThrows()
    {
        // Arrange
        var engine = new DartsRuleEngine("Player1", "Player2");
        
        // Directly set up a finish scenario - player at exactly 40 points
        var player1 = engine.GetPlayers().First(p => p.Id == "Player1");
        player1.Score = 40;
        
        // Finish the game with double 20
        var finishResult = engine.RecordThrow("Player1", "20", "double");
        Assert.IsTrue(finishResult.IsFinish);
        Assert.IsTrue(engine.IsGameOver());

        // Act - Try to throw after game over
        var result = engine.RecordThrow("Player1", "20", "single");

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("Game is already over", result.ErrorMessage);
    }

    [TestMethod]
    public void MultiPlayerFlow_AlternatesTurnsCorrectly()
    {
        // Arrange
        var engine = new DartsRuleEngine("Alice", "Bob");

        // Act & Assert - Verify turn alternation
        Assert.AreEqual("Alice", engine.GetCurrentPlayer().Id);
        
        // Alice's turn
        engine.RecordThrow("Alice", "20", "single");
        engine.RecordThrow("Alice", "19", "single");
        engine.RecordThrow("Alice", "18", "single");
        
        // Should now be Bob's turn
        Assert.AreEqual("Bob", engine.GetCurrentPlayer().Id);
        Assert.AreEqual(0, engine.GetDartsThrown());
        
        // Bob's turn
        engine.RecordThrow("Bob", "15", "single");
        engine.RecordThrow("Bob", "14", "single");
        engine.RecordThrow("Bob", "13", "single");
        
        // Should be back to Alice
        Assert.AreEqual("Alice", engine.GetCurrentPlayer().Id);
        Assert.AreEqual(0, engine.GetDartsThrown());
    }

    [TestMethod]
    public void LegacyMethods_StillWorkForBackwardCompatibility()
    {
        // Act
        var result = DartsRuleEngine.ValidateThrow(501, 60);
        var checkouts = DartsRuleEngine.GetCommonCheckouts(100);
        var canFinish = DartsRuleEngine.CanFinishInDarts(40, 1);

        // Assert
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(441, result.NewScore);
        Assert.IsTrue(checkouts.Count > 0);
        Assert.IsTrue(canFinish);
    }

    [TestMethod]
    public void BustScenarios_ResetScoreToStartOfTurn()
    {
        // Arrange
        var engine = new DartsRuleEngine("Player1", "Player2");
        
        // Throw some darts to establish start-of-turn score
        engine.RecordThrow("Player1", "20", "single"); // 481
        engine.RecordThrow("Player1", "19", "single"); // 462
        
        // Record the current score before setting up bust scenario
        var scoreAfterTwoDarts = engine.GetRemainingScore("Player1"); // Should be 462
        
        // Now set up a guaranteed bust scenario by making the third dart a big score
        var player1 = engine.GetPlayers().First(p => p.Id == "Player1");
        player1.Score = 30; // Set low enough that treble 20 (60) will bust
        
        // Act - Throw a dart that will bust
        var bustResult = engine.RecordThrow("Player1", "20", "treble"); // 60 points

        // Assert
        Assert.IsTrue(bustResult.IsBust);
        // Score should be reset to start of turn, which was 501 at the beginning
        // Since we manipulated the score mid-turn, it resets to the actual start-of-turn score
        Assert.AreEqual(501, bustResult.NewScore); // Start of turn was 501
        Assert.AreEqual("Player2", engine.GetCurrentPlayer().Id);
    }

    [TestMethod]
    public void SetCurrentPlayer_ValidPlayerId_SetsCurrentPlayerCorrectly()
    {
        // Arrange
        var engine = new DartsRuleEngine("Alice", "Bob");
        
        // Act - Set current player to Bob
        var result = engine.SetCurrentPlayer("Bob");
        
        // Assert
        Assert.IsTrue(result);
        Assert.AreEqual("Bob", engine.GetCurrentPlayer().Id);
        Assert.AreEqual(0, engine.GetDartsThrown()); // Should reset darts thrown
    }

    [TestMethod]
    public void SetCurrentPlayer_InvalidPlayerId_ReturnsFalse()
    {
        // Arrange
        var engine = new DartsRuleEngine("Alice", "Bob");
        
        // Act
        var result = engine.SetCurrentPlayer("Charlie");
        
        // Assert
        Assert.IsFalse(result);
        Assert.AreEqual("Alice", engine.GetCurrentPlayer().Id); // Should remain unchanged
    }

    [TestMethod]
    public void IsPossibleDartScore_ValidScores_ReturnsTrue()
    {
        // Test some known valid scores
        Assert.IsTrue(DartsRuleEngine.IsPossibleDartScore(180, 3)); // T20, T20, T20
        Assert.IsTrue(DartsRuleEngine.IsPossibleDartScore(170, 3)); // T20, T20, Bull
        Assert.IsTrue(DartsRuleEngine.IsPossibleDartScore(60, 1));  // T20
        Assert.IsTrue(DartsRuleEngine.IsPossibleDartScore(100, 2)); // T20, D20
        Assert.IsTrue(DartsRuleEngine.IsPossibleDartScore(25, 1));  // Outer Bull
        Assert.IsTrue(DartsRuleEngine.IsPossibleDartScore(50, 1));  // Inner Bull
        Assert.IsTrue(DartsRuleEngine.IsPossibleDartScore(3, 1));   // Single 3
        Assert.IsTrue(DartsRuleEngine.IsPossibleDartScore(40, 1));  // Double 20
    }

    [TestMethod]
    public void IsPossibleDartScore_ImpossibleScores_ReturnsFalse()
    {
        // Test known impossible scores
        Assert.IsFalse(DartsRuleEngine.IsPossibleDartScore(178, 3)); // Impossible with 3 darts
        Assert.IsFalse(DartsRuleEngine.IsPossibleDartScore(176, 3)); // Impossible with 3 darts
        Assert.IsFalse(DartsRuleEngine.IsPossibleDartScore(175, 3)); // Impossible with 3 darts
        Assert.IsFalse(DartsRuleEngine.IsPossibleDartScore(173, 3)); // Impossible with 3 darts
        Assert.IsFalse(DartsRuleEngine.IsPossibleDartScore(172, 3)); // Impossible with 3 darts
        Assert.IsFalse(DartsRuleEngine.IsPossibleDartScore(169, 3)); // Impossible with 3 darts
        Assert.IsFalse(DartsRuleEngine.IsPossibleDartScore(166, 3)); // Impossible with 3 darts
        Assert.IsFalse(DartsRuleEngine.IsPossibleDartScore(163, 3)); // Impossible with 3 darts
        Assert.IsFalse(DartsRuleEngine.IsPossibleDartScore(61, 1));  // Impossible with 1 dart
        Assert.IsFalse(DartsRuleEngine.IsPossibleDartScore(121, 2)); // Impossible with 2 darts
    }

    [TestMethod]
    public void IsPossibleDartScore_EdgeCases_HandledCorrectly()
    {
        // Test edge cases
        Assert.IsFalse(DartsRuleEngine.IsPossibleDartScore(-1, 3));  // Negative score
        Assert.IsFalse(DartsRuleEngine.IsPossibleDartScore(100, 0)); // Zero darts
        Assert.IsFalse(DartsRuleEngine.IsPossibleDartScore(100, 4)); // More than 3 darts
        Assert.IsTrue(DartsRuleEngine.IsPossibleDartScore(0, 1));    // Zero score (miss)
        Assert.IsTrue(DartsRuleEngine.IsPossibleDartScore(1, 1));    // Single 1
    }

    [TestMethod]
    public void ValidateThrow_ImpossibleScore_ReturnsInvalid()
    {
        // Test that the legacy method rejects impossible scores
        var result = DartsRuleEngine.ValidateThrow(501, 178, 3);
        
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("Invalid score: 178 is not possible with 3 darts", result.ErrorMessage);
    }

    [TestMethod]
    public void ValidateThrow_PossibleScore_ReturnsValid()
    {
        // Test that the legacy method accepts possible scores
        var result = DartsRuleEngine.ValidateThrow(501, 180, 3);
        
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(321, result.NewScore); // 501 - 180 = 321
    }
} 