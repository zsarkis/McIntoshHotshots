using McIntoshHotshots.Model;

namespace McIntoshHotshots.Services;

public class DartsRuleEngine
{
    // Valid double checkout scores (must finish on these)
    private static readonly HashSet<int> ValidDoubles = new()
    {
        2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32, 34, 36, 38, 40, 50
    };

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
        
        var newScore = currentScore - turnScore;
        
        // Check for going below zero
        if (newScore < 0)
        {
            result.IsValid = true; // Valid throw, but it's a bust
            result.IsBust = true;
            result.NewScore = currentScore; // Score stays the same
            result.IsFinish = false;
            result.BustReason = "Score would go below zero";
            return result;
        }
        
        // Check for ending on 1 (impossible to finish)
        if (newScore == 1)
        {
            result.IsValid = true; // Valid throw, but it's a bust
            result.IsBust = true;
            result.NewScore = currentScore; // Score stays the same
            result.IsFinish = false;
            result.BustReason = "Cannot finish on 1 (no such thing as double 1)";
            return result;
        }
        
        // Check for finish
        if (newScore == 0)
        {
            // For a finish, the turn must be able to end with a double
            if (!CanFinishWithDouble(turnScore))
            {
                result.IsValid = true; // Valid throw, but it's a bust
                result.IsBust = true;
                result.NewScore = currentScore; // Score stays the same
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
        // For turn-based scoring, we assume the last dart was a double if it matches a valid checkout
        
        // Single dart finishes (just the double)
        if (ValidDoubles.Contains(turnScore))
            return true;
        
        // Check common two-dart finishes
        foreach (var doubleValue in ValidDoubles)
        {
            int remainingScore = turnScore - doubleValue;
            if (remainingScore > 0 && remainingScore <= 60) // Possible with single dart
                return true;
        }
        
        // Check common three-dart finishes  
        foreach (var doubleValue in ValidDoubles)
        {
            int remainingScore = turnScore - doubleValue;
            if (remainingScore > 0 && remainingScore <= 120) // Possible with two darts
                return true;
        }
        
        return false;
    }
    
    private static int? GetLikelyFinishingDouble(int turnScore)
    {
        // Return the most likely finishing double for this turn score
        
        // Direct double finish
        if (ValidDoubles.Contains(turnScore))
            return turnScore;
        
        // Find the highest reasonable double that could be the finish
        foreach (var doubleValue in ValidDoubles.OrderByDescending(x => x))
        {
            int remainingScore = turnScore - doubleValue;
            if (remainingScore > 0 && remainingScore <= 120) // Reasonable remainder
                return doubleValue;
        }
        
        return null;
    }
    
    public static List<int> GetValidCheckouts(int score)
    {
        var validCheckouts = new List<int>();
        
        if (score <= 0 || score == 1) return validCheckouts;
        
        // Direct double finishes
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
            // Can finish in 2 darts if we can hit any score that leaves a valid double
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
            // Most scores under 170 can be finished in 3 darts
            return score <= 170;
        }
        
        return false;
    }
    
    public static List<string> GetCommonCheckouts(int score)
    {
        var checkouts = new List<string>();
        
        // Common checkout combinations
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
            { 32, new() { "D16" } },
            // Add more as needed...
        };
        
        if (commonFinishes.ContainsKey(score))
        {
            checkouts.AddRange(commonFinishes[score]);
        }
        
        return checkouts;
    }
}

public class DartsThrowResult
{
    public bool IsValid { get; set; }
    public bool IsBust { get; set; }
    public bool IsFinish { get; set; }
    public int NewScore { get; set; }
    public int? FinishingDouble { get; set; }
    public string BustReason { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
} 