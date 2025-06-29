using McIntoshHotshots.Model;

namespace McIntoshHotshots.Services;

public interface IUserPerformanceService
{
    Task<UserPerformanceData> GetUserPerformanceDataAsync(string userId, CancellationToken cancellationToken = default);
    Task<HeadToHeadData> GetHeadToHeadDataAsync(string userId, string opponentName, CancellationToken cancellationToken = default);
    Task<List<string>> GetOpponentListAsync(string userId, CancellationToken cancellationToken = default);
    Task<DetailedLegAnalysis> GetDetailedLegAnalysisAsync(string userId, string opponentName, CancellationToken cancellationToken = default);
    Task<FirstNineAnalysis> GetFirstNineAnalysisAsync(string userId, string? opponentName = null, CancellationToken cancellationToken = default);
    Task<ScoreDownToValueAnalysis> GetScoreDownToValueAnalysisAsync(string userId, int targetValue, string? opponentName = null, CancellationToken cancellationToken = default);
    Task<FirstNineAnalysis> GetAnyPlayerFirstNineAnalysisAsync(string playerName, CancellationToken cancellationToken = default);
    Task<ScoreDownToValueAnalysis> GetAnyPlayerScoreDownToValueAnalysisAsync(string playerName, int targetValue, CancellationToken cancellationToken = default);
    Task<UserPerformanceData> GetAnyPlayerPerformanceDataAsync(string playerName, CancellationToken cancellationToken = default);
    Task<List<string>> GetAllPlayerNamesAsync(CancellationToken cancellationToken = default);
    Task<AverageScorePerTurnAnalysis> GetAverageScorePerTurnDownToValueAsync(string userId, int targetValue, string? opponentName = null, CancellationToken cancellationToken = default);
    Task<AverageScorePerTurnAnalysis> GetAnyPlayerAverageScorePerTurnDownToValueAsync(string playerName, int targetValue, CancellationToken cancellationToken = default);
    Task<ScoreDownToValueAnalysis> GetDartsToWinFromValueAnalysisAsync(string userId, int startingValue, string? opponentName = null, CancellationToken cancellationToken = default);
    Task<ScoreDownToValueAnalysis> GetAnyPlayerDartsToWinFromValueAnalysisAsync(string playerName, int startingValue, CancellationToken cancellationToken = default);
    Task<FinishingAttemptsAnalysis> GetFinishingAttemptsFromValueAnalysisAsync(string userId, int startingValue, string? opponentName = null, CancellationToken cancellationToken = default);
    Task<FinishingAttemptsAnalysis> GetAnyPlayerFinishingAttemptsFromValueAnalysisAsync(string playerName, int startingValue, CancellationToken cancellationToken = default);
    Task<BestLegAnalysis> GetBestLegAnalysisAsync(string userId, string? opponentName = null, CancellationToken cancellationToken = default);
    Task<BestLegAnalysis> GetAnyPlayerBestLegAnalysisAsync(string playerName, CancellationToken cancellationToken = default);
}

public class BestLegAnalysis
{
    public string? OpponentName { get; set; }
    public int BestLegDarts { get; set; }
    public int WorstLegDarts { get; set; }
    public double AverageDartsPerLeg { get; set; }
    public int TotalLegsWon { get; set; }
    public int HighestFinish { get; set; } // This is separate from best leg - it's the highest checkout in any leg
    public List<string> Insights { get; set; } = new();
    public List<int> AllLegDartCounts { get; set; } = new(); // All winning legs in darts
    public bool IsOpponentComparison { get; set; }
    public int OpponentBestLegDarts { get; set; }
    public int OpponentWorstLegDarts { get; set; }
    public double OpponentAverageDartsPerLeg { get; set; }
    public int OpponentTotalLegsWon { get; set; }
    public List<int> OpponentAllLegDartCounts { get; set; } = new();
    public string Winner { get; set; } = "";
    public double Difference { get; set; }
}

public class DetailedLegAnalysis
{
    public string OpponentName { get; set; } = "";
    public int TotalLegs { get; set; }
    public List<string> CommonScoresLeft { get; set; } = new();
    public List<string> MissedCheckouts { get; set; } = new();
    public List<string> SuccessfulCheckouts { get; set; } = new();
    public double CheckoutSuccessRate { get; set; }
    public List<string> ScoringPatterns { get; set; } = new();
    public List<string> WeakAreas { get; set; } = new();
    public List<string> Strengths { get; set; } = new();
    public List<string> SpecificInsights { get; set; } = new();
    public double AverageTurnsPerLeg { get; set; }
    public int HighestFinish { get; set; }
    public List<string> TurnAnalysis { get; set; } = new();
}

public class FirstNineAnalysis
{
    public string? OpponentName { get; set; }
    public double PlayerFirstNineAverage { get; set; }
    public double OpponentFirstNineAverage { get; set; }
    public int TotalLegsAnalyzed { get; set; }
    public List<double> PlayerFirstNineAverages { get; set; } = new(); // Average per 3 darts for each leg
    public List<double> OpponentFirstNineAverages { get; set; } = new(); // Average per 3 darts for each leg
    public string Winner { get; set; } = "";
    public double Difference { get; set; }
    public List<string> Insights { get; set; } = new();
    public bool IsOpponentComparison { get; set; }
}

public class ScoreDownToValueAnalysis
{
    public string? OpponentName { get; set; }
    public int TargetValue { get; set; }
    public double PlayerAverageDarts { get; set; }
    public double OpponentAverageDarts { get; set; }
    public int TotalLegsAnalyzed { get; set; }
    public List<int> PlayerDartCounts { get; set; } = new();
    public List<int> OpponentDartCounts { get; set; } = new();
    public string Winner { get; set; } = "";
    public double Difference { get; set; }
    public List<string> Insights { get; set; } = new();
    public bool IsOpponentComparison { get; set; }
    public int PlayerFastestDarts { get; set; }
    public int PlayerSlowestDarts { get; set; }
    public int OpponentFastestDarts { get; set; }
    public int OpponentSlowestDarts { get; set; }
}

public class AverageScorePerTurnAnalysis
{
    public string? OpponentName { get; set; }
    public int TargetValue { get; set; }
    public double PlayerAverageScorePerTurn { get; set; }
    public double OpponentAverageScorePerTurn { get; set; }
    public int TotalLegsAnalyzed { get; set; }
    public List<double> PlayerScorePerTurnAverages { get; set; } = new(); // Average score per turn for each leg
    public List<double> OpponentScorePerTurnAverages { get; set; } = new(); // Average score per turn for each leg
    public string Winner { get; set; } = "";
    public double Difference { get; set; }
    public List<string> Insights { get; set; } = new();
    public bool IsOpponentComparison { get; set; }
    public double PlayerBestLegAverage { get; set; }
    public double PlayerWorstLegAverage { get; set; }
    public double OpponentBestLegAverage { get; set; }
    public double OpponentWorstLegAverage { get; set; }
}

public class FinishingAttemptsAnalysis
{
    public string? OpponentName { get; set; }
    public int StartingValue { get; set; }
    public int TotalAttempts { get; set; }
    public int SuccessfulFinishes { get; set; }
    public int FailedAttempts { get; set; }
    public double SuccessRate { get; set; }
    public double AverageDartsInWins { get; set; }
    public double AverageDartsInLosses { get; set; }
    public List<int> WinningAttemptDarts { get; set; } = new();
    public List<int> LosingAttemptDarts { get; set; } = new();
    public double FastestSuccessfulFinish { get; set; }
    public double SlowestSuccessfulFinish { get; set; }
    public double AverageDartsInFailedAttempts { get; set; }
    public List<string> Insights { get; set; } = new();
    public bool IsOpponentComparison { get; set; }
    public int OpponentTotalAttempts { get; set; }
    public int OpponentSuccessfulFinishes { get; set; }
    public double OpponentSuccessRate { get; set; }
    public double OpponentAverageDartsInWins { get; set; }
    public double OpponentAverageDartsInLosses { get; set; }
}

public class UserPerformanceData
{
    public PlayerModel? Player { get; set; }
    public List<MatchSummaryModel> RecentMatches { get; set; } = new();
    public List<LegDetailModel> RecentLegDetails { get; set; } = new();
    public PerformanceStats Stats { get; set; } = new();
    public HeadToHeadData? HeadToHead { get; set; }
}

public class PerformanceStats
{
    public double AverageScore { get; set; }
    public int TotalMatches { get; set; }
    public int MatchesWon { get; set; }
    public int TotalLegs { get; set; }
    public int UniqueOpponents { get; set; } // Number of different opponents played against
    public double WinPercentage { get; set; }
    public double AverageThreeDartScore { get; set; }
    public double CheckoutPercentage { get; set; }
    public int HighestFinish { get; set; }
    public double AverageTurnsPerLeg { get; set; }
    public List<int> CommonWeakScores { get; set; } = new();
    public List<string> PerformanceTrends { get; set; } = new();
}

public class HeadToHeadData
{
    public string OpponentName { get; set; } = "";
    public int TotalMatches { get; set; }
    public int MatchesWon { get; set; }
    public int MatchesLost { get; set; }
    public double WinPercentage { get; set; }
    public double AverageScoreVsOpponent { get; set; }
    public double OpponentAverageScore { get; set; }
    public int LegsWon { get; set; }
    public int LegsLost { get; set; }
    public double LegWinPercentage { get; set; }
    public string LastMatchResult { get; set; } = "";
    public List<string> PerformanceTrends { get; set; } = new();
} 