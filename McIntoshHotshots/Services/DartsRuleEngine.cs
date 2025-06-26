using McIntoshHotshots.Model;

namespace McIntoshHotshots.Services;

/// <summary>
/// Comprehensive 501 Darts rule engine supporting full game state management,
/// individual dart processing, and advanced game logic.
/// </summary>
public class DartsRuleEngine
{
    // Valid double checkout scores (must finish on these)
    private static readonly HashSet<int> ValidDoubles = new()
    {
        2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32, 34, 36, 38, 40, 50
    };

    private readonly List<DartsPlayer> _players;
    private int _currentPlayerIndex;
    private int _dartsThrown;
    private int _startOfTurnScore;
    private bool _gameOver;
    private string? _winnerId;

    /// <summary>
    /// Initialize a new 501 Darts game with two players.
    /// </summary>
    /// <param name="player1Id">First player identifier</param>
    /// <param name="player2Id">Second player identifier</param>
    public DartsRuleEngine(string player1Id, string player2Id)
    {
        if (string.IsNullOrWhiteSpace(player1Id) || string.IsNullOrWhiteSpace(player2Id))
            throw new ArgumentException("Player IDs cannot be null or empty");

        if (player1Id == player2Id)
            throw new ArgumentException("Player IDs must be unique");

        _players = new List<DartsPlayer>
        {
            new() { Id = player1Id, Score = 501 },
            new() { Id = player2Id, Score = 501 }
        };

        _currentPlayerIndex = 0;
        _dartsThrown = 0;
        _startOfTurnScore = 501;
        _gameOver = false;
        _winnerId = null;
    }

    /// <summary>
    /// Record a single dart throw and process game logic.
    /// </summary>
    /// <param name="playerId">Player making the throw</param>
    /// <param name="segment">Dart board segment (1-20, 25, or "bull")</param>
    /// <param name="ringType">Ring type ("fat-single", "skinny-single", "double", "treble", or "single" for generic)</param>
    /// <returns>Result of the throw with game state information</returns>
    public DartsThrowResult RecordThrow(string playerId, string segment, string ringType)
    {
        var result = new DartsThrowResult();

        // Validate game state
        if (_gameOver)
        {
            result.IsValid = false;
            result.ErrorMessage = "Game is already over";
            return result;
        }

        // Validate player turn
        var currentPlayer = GetCurrentPlayer();
        if (currentPlayer.Id != playerId)
        {
            result.IsValid = false;
            result.ErrorMessage = $"Not {playerId}'s turn. Current player: {currentPlayer.Id}";
            return result;
        }

        // Start of turn - record starting score
        if (_dartsThrown == 0)
        {
            _startOfTurnScore = currentPlayer.Score;
        }

        // Calculate dart points
        var dartPoints = CalculateDartPoints(segment, ringType);
        if (dartPoints == -1)
        {
            result.IsValid = false;
            result.ErrorMessage = "Invalid segment or ring type combination";
            return result;
        }

        _dartsThrown++;

        // Tentatively subtract points
        var newScore = currentPlayer.Score - dartPoints;

        // Check for bust conditions
        if (IsBust(newScore, ringType))
        {
            result = HandleBust(currentPlayer, dartPoints, newScore, segment, ringType);
            EndTurn();
            return result;
        }

        // Check for finish (exactly 0 with double or bullseye)
        if (newScore == 0 && IsValidFinish(ringType))
        {
            currentPlayer.Score = 0;
            _gameOver = true;
            _winnerId = playerId;

            result.IsValid = true;
            result.IsBust = false;
            result.IsFinish = true;
            result.NewScore = 0;
            result.DartPoints = dartPoints;
            result.IsGameOver = true;
            result.WinnerId = playerId;
            result.DartsThrown = _dartsThrown;
            result.RingType = ringType;
            result.Segment = segment;
            return result;
        }

        // Valid throw, update score
        currentPlayer.Score = newScore;

        // Check if turn should end (3 darts thrown)
        if (_dartsThrown >= 3)
        {
            EndTurn();
        }

        result.IsValid = true;
        result.IsBust = false;
        result.IsFinish = false;
        result.NewScore = newScore;
        result.DartPoints = dartPoints;
        result.IsGameOver = false;
        result.DartsThrown = _dartsThrown;
        result.CurrentPlayerId = GetCurrentPlayer().Id;
        result.RingType = ringType;
        result.Segment = segment;

        return result;
    }

    /// <summary>
    /// Get the remaining score for a player.
    /// </summary>
    public int GetRemainingScore(string playerId)
    {
        var player = _players.FirstOrDefault(p => p.Id == playerId);
        return player?.Score ?? -1;
    }

    /// <summary>
    /// Check if the game is over.
    /// </summary>
    public bool IsGameOver() => _gameOver;

    /// <summary>
    /// Get the winner of the game (null if game not over).
    /// </summary>
    public string? GetWinner() => _winnerId;

    /// <summary>
    /// Get the current player whose turn it is.
    /// </summary>
    public DartsPlayer GetCurrentPlayer() => _players[_currentPlayerIndex];

    /// <summary>
    /// Get all players in the game.
    /// </summary>
    public IReadOnlyList<DartsPlayer> GetPlayers() => _players.AsReadOnly();

    /// <summary>
    /// Get the number of darts thrown in the current turn.
    /// </summary>
    public int GetDartsThrown() => _dartsThrown;

    /// <summary>
    /// Manually advance to the next player's turn.
    /// </summary>
    public void NextTurn()
    {
        if (!_gameOver)
        {
            EndTurn();
        }
    }

    /// <summary>
    /// Set the current player by player ID (for synchronization purposes).
    /// </summary>
    /// <param name="playerId">The player ID to set as current</param>
    /// <returns>True if successful, false if player not found</returns>
    public bool SetCurrentPlayer(string playerId)
    {
        if (_gameOver) return false;
        
        for (int i = 0; i < _players.Count; i++)
        {
            if (_players[i].Id == playerId)
            {
                _currentPlayerIndex = i;
                _dartsThrown = 0; // Reset darts thrown for the new turn
                _startOfTurnScore = _players[i].Score;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Get checkout recommendations for a given score.
    /// </summary>
    public List<CheckoutRecommendation> GetCheckoutRecommendations(int score)
    {
        var recommendations = new List<CheckoutRecommendation>();

        if (score <= 0 || score == 1 || score > 170) return recommendations;

        // Single dart finishes
        if (ValidDoubles.Contains(score))
        {
            recommendations.Add(new CheckoutRecommendation
            {
                Darts = 1,
                Combination = GetSegmentAndRing(score, true),
                Description = $"Double {score / 2}"
            });
        }

        // Two dart finishes
        for (int firstDart = 1; firstDart <= 60; firstDart++)
        {
            int remaining = score - firstDart;
            if (ValidDoubles.Contains(remaining))
            {
                recommendations.Add(new CheckoutRecommendation
                {
                    Darts = 2,
                    Combination = $"{GetBestSingleScore(firstDart)}, {GetSegmentAndRing(remaining, true)}",
                    Description = $"{firstDart} + Double {remaining / 2}"
                });
                if (recommendations.Count >= 5) break; // Limit recommendations
            }
        }

        return recommendations.OrderBy(r => r.Darts).Take(3).ToList();
    }

    #region Private Helper Methods

    private int CalculateDartPoints(string segment, string ringType)
    {
        int baseValue;

        // Handle special segments
        if (segment.ToLower() == "bull")
        {
            baseValue = 25;
        }
        else if (segment == "25")
        {
            baseValue = 25;
        }
        else if (int.TryParse(segment, out int segmentValue) && segmentValue >= 1 && segmentValue <= 20)
        {
            baseValue = segmentValue;
        }
        else
        {
            return -1; // Invalid segment
        }

        // Apply ring multiplier
        return ringType.ToLower() switch
        {
            "single" => baseValue,      // Generic single (backward compatibility)
            "fat-single" => baseValue,  // Wide single area (between double and treble)
            "skinny-single" => baseValue, // Narrow single area (between treble and outer bull)
            "double" => baseValue * 2,
            "treble" => baseValue * 3,
            _ => -1 // Invalid ring type
        };
    }

    private bool IsBust(int newScore, string ringType)
    {
        // Bust if score goes below 0
        if (newScore < 0) return true;

        // Bust if score is exactly 1 (impossible to finish)
        if (newScore == 1) return true;

        // Bust if score is 0 but not finished with double or bullseye
        if (newScore == 0 && !IsValidFinish(ringType)) return true;

        return false;
    }

    private bool IsValidFinish(string ringType)
    {
        return ringType.ToLower() == "double";
    }

    private DartsThrowResult HandleBust(DartsPlayer player, int dartPoints, int newScore, string segment, string ringType)
    {
        // Reset score to start of turn
        player.Score = _startOfTurnScore;

        var result = new DartsThrowResult
        {
            IsValid = true,
            IsBust = true,
            IsFinish = false,
            NewScore = _startOfTurnScore,
            DartPoints = dartPoints,
            DartsThrown = _dartsThrown,
            CurrentPlayerId = GetCurrentPlayer().Id,
            RingType = ringType,
            Segment = segment
        };

        // Determine bust reason
        if (newScore < 0)
        {
            result.BustReason = "Score would go below zero";
        }
        else if (newScore == 1)
        {
            result.BustReason = "Cannot finish on 1 (no double 0.5)";
        }
        else if (newScore == 0)
        {
            result.BustReason = "Must finish with a double";
        }

        return result;
    }

    private void EndTurn()
    {
        _dartsThrown = 0;
        _currentPlayerIndex = (_currentPlayerIndex + 1) % _players.Count;
        _startOfTurnScore = GetCurrentPlayer().Score;
    }

    private string GetSegmentAndRing(int points, bool isDouble)
    {
        if (isDouble)
        {
            return $"D{points / 2}";
        }
        return points.ToString();
    }

    private string GetBestSingleScore(int points)
    {
        // Return the most efficient way to score these points with a single dart
        if (points <= 20) return $"S{points}";
        if (points <= 40 && points % 2 == 0) return $"D{points / 2}";
        if (points <= 60 && points % 3 == 0) return $"T{points / 3}";
        
        // For odd numbers > 20, suggest combinations
        if (points == 25) return "Bull (outer)";
        if (points == 50) return "Bull (inner)";
        
        return points.ToString();
    }

    #endregion

    #region Static Legacy Methods (for backward compatibility)

    /// <summary>
    /// Legacy static method for validating complete turns (maintained for backward compatibility).
    /// </summary>
    public static DartsThrowResult ValidateThrow(int currentScore, int turnScore, int dartsUsed = 3)
    {
        var result = new DartsThrowResult();
        
        // Basic validation
        if (turnScore < 0)
        {
            result.IsValid = false;
            result.ErrorMessage = "Invalid score: Cannot be negative";
            return result;
        }
        
        if (turnScore > 180)
        {
            result.IsValid = false;
            result.ErrorMessage = "Invalid score: Maximum possible is 180";
            return result;
        }
        
        // Check for impossible dart scores
        if (!IsPossibleDartScore(turnScore, dartsUsed))
        {
            result.IsValid = false;
            result.ErrorMessage = $"Invalid score: {turnScore} is not possible with {dartsUsed} dart{(dartsUsed == 1 ? "" : "s")}";
            return result;
        }
        
        var newScore = currentScore - turnScore;
        
        // Check for going below zero
        if (newScore < 0)
        {
            result.IsValid = true;
            result.IsBust = true;
            result.NewScore = currentScore;
            result.IsFinish = false;
            result.BustReason = "Score would go below zero";
            return result;
        }
        
        // Check for ending on 1
        if (newScore == 1)
        {
            result.IsValid = true;
            result.IsBust = true;
            result.NewScore = currentScore;
            result.IsFinish = false;
            result.BustReason = "Cannot finish on 1 (no such thing as double 1)";
            return result;
        }
        
        // Check for finish
        if (newScore == 0)
        {
            if (!CanFinishWithDouble(turnScore))
            {
                result.IsValid = true;
                result.IsBust = true;
                result.NewScore = currentScore;
                result.IsFinish = false;
                result.BustReason = "Must finish with a double. This score cannot end with a double.";
                return result;
            }
            
            result.IsValid = true;
            result.IsBust = false;
            result.NewScore = 0;
            result.IsFinish = true;
            result.FinishingDouble = GetLikelyFinishingDouble(turnScore);
            return result;
        }
        
        // Valid throw, game continues
        result.IsValid = true;
        result.IsBust = false;
        result.NewScore = newScore;
        result.IsFinish = false;
        return result;
    }
    
    private static bool CanFinishWithDouble(int turnScore)
    {
        // Check if this turn score could reasonably end with a double
        if (ValidDoubles.Contains(turnScore))
            return true;
        
        // Check common two-dart finishes
        foreach (var doubleValue in ValidDoubles)
        {
            int remainingScore = turnScore - doubleValue;
            if (remainingScore > 0 && remainingScore <= 60)
                return true;
        }
        
        // Check common three-dart finishes  
        foreach (var doubleValue in ValidDoubles)
        {
            int remainingScore = turnScore - doubleValue;
            if (remainingScore > 0 && remainingScore <= 120)
                return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Determines if a given score is mathematically possible with the specified number of darts.
    /// </summary>
    /// <param name="score">The total score to validate</param>
    /// <param name="darts">Number of darts used (1-3)</param>
    /// <returns>True if the score is possible, false otherwise</returns>
    public static bool IsPossibleDartScore(int score, int darts)
    {
        if (score < 0 || darts < 1 || darts > 3)
            return false;
            
        // Generate all possible scores for the given number of darts
        var possibleScores = GeneratePossibleScores(darts);
        return possibleScores.Contains(score);
    }
    
    /// <summary>
    /// Generates all mathematically possible dart scores for a given number of darts.
    /// </summary>
    /// <param name="darts">Number of darts (1-3)</param>
    /// <returns>HashSet of all possible scores</returns>
    private static HashSet<int> GeneratePossibleScores(int darts)
    {
        // Cache for performance
        if (_possibleScoresCache.ContainsKey(darts))
            return _possibleScoresCache[darts];
            
        var possibleScores = new HashSet<int>();
        
        // All possible single dart scores
        var singleDartScores = new List<int>();
        
        // Miss (0 points)
        singleDartScores.Add(0);
        
        // Singles: 1-20
        for (int i = 1; i <= 20; i++)
            singleDartScores.Add(i);
            
        // Doubles: 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32, 34, 36, 38, 40
        for (int i = 1; i <= 20; i++)
            singleDartScores.Add(i * 2);
            
        // Trebles: 3, 6, 9, 12, 15, 18, 21, 24, 27, 30, 33, 36, 39, 42, 45, 48, 51, 54, 57, 60
        for (int i = 1; i <= 20; i++)
            singleDartScores.Add(i * 3);
            
        // Bull areas: 25 (outer bull), 50 (inner bull/bullseye)
        singleDartScores.Add(25);
        singleDartScores.Add(50);
        
        // Remove duplicates and sort
        singleDartScores = singleDartScores.Distinct().OrderBy(x => x).ToList();
        
        if (darts == 1)
        {
            possibleScores.UnionWith(singleDartScores);
        }
        else if (darts == 2)
        {
            // All combinations of 2 darts
            foreach (var dart1 in singleDartScores)
            {
                foreach (var dart2 in singleDartScores)
                {
                    possibleScores.Add(dart1 + dart2);
                }
            }
        }
        else if (darts == 3)
        {
            // All combinations of 3 darts
            foreach (var dart1 in singleDartScores)
            {
                foreach (var dart2 in singleDartScores)
                {
                    foreach (var dart3 in singleDartScores)
                    {
                        possibleScores.Add(dart1 + dart2 + dart3);
                    }
                }
            }
        }
        
        // Cache the result
        _possibleScoresCache[darts] = possibleScores;
        return possibleScores;
    }
    
    // Cache for possible scores to avoid recalculating
    private static readonly Dictionary<int, HashSet<int>> _possibleScoresCache = new();
    
    private static int? GetLikelyFinishingDouble(int turnScore)
    {
        // Direct double finish
        if (ValidDoubles.Contains(turnScore))
            return turnScore;
        
        // Find the highest reasonable double that could be the finish
        foreach (var doubleValue in ValidDoubles.OrderByDescending(x => x))
        {
            int remainingScore = turnScore - doubleValue;
            if (remainingScore > 0 && remainingScore <= 120)
                return doubleValue;
        }
        
        return null;
    }
    
    public static List<int> GetValidCheckouts(int score)
    {
        var validCheckouts = new List<int>();
        
        if (score <= 0 || score == 1) return validCheckouts;
        
        if (ValidDoubles.Contains(score))
        {
            validCheckouts.Add(score);
        }
        
        return validCheckouts;
    }
    
    public static bool CanFinishInDarts(int score, int darts)
    {
        if (score <= 0 || score == 1) return false;
        if (darts <= 0) return false;
        
        if (darts == 1)
        {
            return ValidDoubles.Contains(score);
        }
        
        if (darts == 2)
        {
            for (int firstDart = 1; firstDart <= 60; firstDart++)
            {
                int remaining = score - firstDart;
                if (ValidDoubles.Contains(remaining))
                {
                    return true;
                }
            }
        }
        
        if (darts == 3)
        {
            return score <= 170;
        }
        
        return false;
    }
    
    public static List<string> GetCommonCheckouts(int score)
    {
        var checkouts = new List<string>();
        
        var commonFinishes = new Dictionary<int, List<string>>
        {
            { 170, new() { "T20, T20, Bull" } },
            { 167, new() { "T20, T19, Bull" } },
            { 164, new() { "T20, T18, Bull", "T19, T19, Bull" } },
            { 161, new() { "T20, T17, Bull" } },
            { 160, new() { "T20, T20, D20" } },
            { 158, new() { "T20, T20, D19" } },
            { 157, new() { "T20, T19, D20" } },
            { 156, new() { "T20, T20, D18" } },
            { 155, new() { "T20, T19, D19" } },
            { 154, new() { "T20, T18, D20" } },
            { 153, new() { "T20, T19, D18" } },
            { 152, new() { "T20, T20, D16" } },
            { 151, new() { "T20, T17, D20" } },
            { 150, new() { "T20, T18, D18" } },
            { 100, new() { "T20, D20" } },
            { 81, new() { "T19, D12" } },
            { 80, new() { "T20, D10" } },
            { 60, new() { "S20, D20" } },
            { 50, new() { "Bull" } },
            { 40, new() { "D20" } },
            { 32, new() { "D16" } }
        };
        
        if (commonFinishes.ContainsKey(score))
        {
            checkouts.AddRange(commonFinishes[score]);
        }
        
        return checkouts;
    }

    #endregion
}

/// <summary>
/// Represents a player in the darts game.
/// </summary>
public class DartsPlayer
{
    public string Id { get; set; } = string.Empty;
    public int Score { get; set; } = 501;
}

/// <summary>
/// Result of a dart throw with comprehensive game state information.
/// </summary>
public class DartsThrowResult
{
    public bool IsValid { get; set; }
    public bool IsBust { get; set; }
    public bool IsFinish { get; set; }
    public bool IsGameOver { get; set; }
    public int NewScore { get; set; }
    public int DartPoints { get; set; }
    public int DartsThrown { get; set; }
    public string CurrentPlayerId { get; set; } = string.Empty;
    public string? WinnerId { get; set; }
    public int? FinishingDouble { get; set; }
    public string BustReason { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string RingType { get; set; } = string.Empty; // Track specific ring type (fat-single, skinny-single, etc.)
    public string Segment { get; set; } = string.Empty;  // Track specific segment hit
}

/// <summary>
/// Represents a checkout recommendation.
/// </summary>
public class CheckoutRecommendation
{
    public int Darts { get; set; }
    public string Combination { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
} 