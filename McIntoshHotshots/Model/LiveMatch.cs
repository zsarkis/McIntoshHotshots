namespace McIntoshHotshots.Model;

public class LiveMatch
{
    public int Id { get; set; }
    public int HomePlayerId { get; set; }
    public int AwayPlayerId { get; set; }
    public string HomePlayerName { get; set; } = string.Empty;
    public string AwayPlayerName { get; set; } = string.Empty;
    public int? TournamentId { get; set; }
    public DateTime StartTime { get; set; }
    public bool IsFinished { get; set; }
    
    // Current leg state
    public int CurrentLegNumber { get; set; } = 1;
    public int HomeLegsWon { get; set; }
    public int AwayLegsWon { get; set; }
    public int HomeCurrentScore { get; set; } = 501;
    public int AwayCurrentScore { get; set; } = 501;
    
    // Current turn
    public int CurrentPlayerId { get; set; }
    public int CurrentTurnNumber { get; set; } = 1;
    public int DartsThrown { get; set; } = 0;
    public List<int> CurrentTurnScores { get; set; } = new();
    
    // Match history
    public List<LiveLeg> Legs { get; set; } = new();
    public List<LiveThrow> AllThrows { get; set; } = new();
    
    // Statistics
    public double HomeSetAverage => CalculateSetAverage(HomePlayerId);
    public double AwaySetAverage => CalculateSetAverage(AwayPlayerId);
    
    private double CalculateSetAverage(int playerId)
    {
        var playerThrows = AllThrows.Where(t => t.PlayerId == playerId && t.LegNumber == CurrentLegNumber).ToList();
        if (!playerThrows.Any()) return 0;
        
        var totalScore = playerThrows.Sum(t => t.Score);
        var totalDarts = playerThrows.Sum(t => t.DartsUsed);
        
        return totalDarts > 0 ? (double)totalScore / totalDarts * 3 : 0;
    }
    
    public bool IsCurrentPlayerTurn(int playerId) => CurrentPlayerId == playerId;
    public bool CanThrow => DartsThrown < 3 && !IsLegFinished();
    public bool IsLegFinished() => HomeCurrentScore == 0 || AwayCurrentScore == 0;
    public int GetPlayerScore(int playerId) => playerId == HomePlayerId ? HomeCurrentScore : AwayCurrentScore;
    public string GetPlayerName(int playerId) => playerId == HomePlayerId ? HomePlayerName : AwayPlayerName;
}

public class LiveLeg
{
    public int LegNumber { get; set; }
    public int WinnerPlayerId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public List<LiveThrow> Throws { get; set; } = new();
}

public class LiveThrow
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public int LegNumber { get; set; }
    public int TurnNumber { get; set; }
    public int Score { get; set; }
    public int DartsUsed { get; set; }
    public int ScoreRemainingBefore { get; set; }
    public int ScoreRemainingAfter { get; set; }
    public DateTime Timestamp { get; set; }
    public bool IsFinishingThrow { get; set; }
    public bool IsBust { get; set; }
    public string BustReason { get; set; } = string.Empty;
    public int? FinishingDouble { get; set; }
} 