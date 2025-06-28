using McIntoshHotshots.Model;

namespace McIntoshHotshots.Services;

public interface IUserPerformanceService
{
    Task<UserPerformanceData> GetUserPerformanceDataAsync(string userId, CancellationToken cancellationToken = default);
    Task<HeadToHeadData> GetHeadToHeadDataAsync(string userId, string opponentName, CancellationToken cancellationToken = default);
    Task<List<string>> GetOpponentListAsync(string userId, CancellationToken cancellationToken = default);
}

public class UserPerformanceData
{
    public PlayerModel Player { get; set; }
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