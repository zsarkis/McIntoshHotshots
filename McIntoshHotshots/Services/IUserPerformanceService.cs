using McIntoshHotshots.Model;

namespace McIntoshHotshots.Services;

public interface IUserPerformanceService
{
    Task<UserPerformanceData> GetUserPerformanceDataAsync(string userId, CancellationToken cancellationToken = default);
}

public class UserPerformanceData
{
    public PlayerModel Player { get; set; }
    public List<MatchSummaryModel> RecentMatches { get; set; } = new();
    public List<LegDetailModel> RecentLegDetails { get; set; } = new();
    public PerformanceStats Stats { get; set; } = new();
}

public class PerformanceStats
{
    public double AverageScore { get; set; }
    public int TotalMatches { get; set; }
    public int MatchesWon { get; set; }
    public int TotalLegs { get; set; }
    public double WinPercentage { get; set; }
    public double AverageThreeDartScore { get; set; }
    public double CheckoutPercentage { get; set; }
    public int HighestFinish { get; set; }
    public double AverageTurnsPerLeg { get; set; }
    public List<int> CommonWeakScores { get; set; } = new();
    public List<string> PerformanceTrends { get; set; } = new();
} 