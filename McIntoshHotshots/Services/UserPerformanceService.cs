using McIntoshHotshots.Model;
using McIntoshHotshots.Repo;

namespace McIntoshHotshots.Services;

public class UserPerformanceService : IUserPerformanceService
{
    private readonly IPlayerRepo _playerRepo;
    private readonly IMatchSummaryRepo _matchSummaryRepo;
    private readonly ILegDetailRepo _legDetailRepo;
    private readonly ILogger<UserPerformanceService> _logger;

    public UserPerformanceService(
        IPlayerRepo playerRepo,
        IMatchSummaryRepo matchSummaryRepo,
        ILegDetailRepo legDetailRepo,
        ILogger<UserPerformanceService> logger)
    {
        _playerRepo = playerRepo;
        _matchSummaryRepo = matchSummaryRepo;
        _legDetailRepo = legDetailRepo;
        _logger = logger;
    }

    public async Task<UserPerformanceData> GetUserPerformanceDataAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get player record
            var player = await _playerRepo.GetPlayerByUserIdAsync(userId);
            if (player == null)
            {
                _logger.LogWarning("No player found for user ID: {UserId}", userId);
                return new UserPerformanceData();
            }

            // Get recent matches (last 10)
            var allMatches = await _matchSummaryRepo.GetMatchesByPlayerIdAsync(player.Id);
            var recentMatches = allMatches.Take(10).ToList();

            // Get recent leg details (last 50 turns)
            var allLegDetails = await _legDetailRepo.GetLegDetailsByPlayerIdAsync(player.Id);
            var recentLegDetails = allLegDetails.TakeLast(50).ToList();

            // Calculate performance statistics
            var stats = CalculatePerformanceStats(player, allMatches, allLegDetails);

            return new UserPerformanceData
            {
                Player = player,
                RecentMatches = recentMatches,
                RecentLegDetails = recentLegDetails,
                Stats = stats
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance data for user: {UserId}", userId);
            return new UserPerformanceData();
        }
    }

    private PerformanceStats CalculatePerformanceStats(PlayerModel player, List<MatchSummaryModel> matches, List<LegDetailModel> legDetails)
    {
        var stats = new PerformanceStats();

        if (!matches.Any()) return stats;

        // Basic match stats
        stats.TotalMatches = matches.Count;
        stats.MatchesWon = matches.Count(m => 
            (m.HomePlayerId == player.Id && m.HomeLegsWon > m.AwayLegsWon) ||
            (m.AwayPlayerId == player.Id && m.AwayLegsWon > m.HomeLegsWon));
        stats.WinPercentage = stats.TotalMatches > 0 ? (double)stats.MatchesWon / stats.TotalMatches * 100 : 0;

        // Calculate unique opponents
        var opponentIds = matches.Select(m => 
            m.HomePlayerId == player.Id ? m.AwayPlayerId : m.HomePlayerId).Distinct().ToList();
        stats.UniqueOpponents = opponentIds.Count;

        // Calculate average score from matches
        var playerAverages = matches.Select(m => 
            m.HomePlayerId == player.Id ? m.HomeSetAverage : m.AwaySetAverage).ToList();
        stats.AverageScore = playerAverages.Any() ? playerAverages.Average() : 0;

        // Leg detail analysis
        if (legDetails.Any())
        {
            stats.TotalLegs = legDetails.Select(ld => ld.LegId).Distinct().Count();
            
            // Average three-dart score
            var validScores = legDetails.Where(ld => ld.DartsUsed == 3 && ld.Score > 0).ToList();
            stats.AverageThreeDartScore = validScores.Any() ? validScores.Average(ld => ld.Score) : 0;

            // Calculate checkout percentage (legs where final score reduced to 0)
            var checkoutAttempts = legDetails.Where(ld => ld.ScoreRemainingBeforeThrow.HasValue && ld.ScoreRemainingBeforeThrow <= 170 && ld.ScoreRemainingBeforeThrow > 0).ToList();
            var successfulCheckouts = checkoutAttempts.Where(ld => ld.ScoreRemainingBeforeThrow == ld.Score).ToList();
            stats.CheckoutPercentage = checkoutAttempts.Any() ? (double)successfulCheckouts.Count / checkoutAttempts.Count * 100 : 0;

            // Highest finish
            stats.HighestFinish = successfulCheckouts.Any() ? successfulCheckouts.Max(ld => ld.Score) : 0;

            // Average turns per leg
            var turnsPerLeg = legDetails.GroupBy(ld => ld.LegId)
                .Select(g => g.Count()).ToList();
            stats.AverageTurnsPerLeg = turnsPerLeg.Any() ? turnsPerLeg.Average() : 0;

            // Identify common weak scores (scores often left after throws)
            var remainingScores = legDetails
                .Where(ld => ld.ScoreRemainingBeforeThrow.HasValue && ld.ScoreRemainingBeforeThrow.Value - ld.Score > 0)
                .Select(ld => ld.ScoreRemainingBeforeThrow!.Value - ld.Score)
                .GroupBy(score => score)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => g.Key)
                .ToList();
            stats.CommonWeakScores = remainingScores;

            // Generate performance trends
            stats.PerformanceTrends = GeneratePerformanceTrends(stats, legDetails);
        }

        return stats;
    }

    private List<string> GeneratePerformanceTrends(PerformanceStats stats, List<LegDetailModel> legDetails)
    {
        var trends = new List<string>();

        // Analyze recent performance trend
        if (legDetails.Count >= 20)
        {
            var recent = legDetails.TakeLast(10).Where(ld => ld.DartsUsed == 3 && ld.Score > 0);
            var earlier = legDetails.Skip(legDetails.Count - 20).Take(10).Where(ld => ld.DartsUsed == 3 && ld.Score > 0);

            if (recent.Any() && earlier.Any())
            {
                var recentAvg = recent.Average(ld => ld.Score);
                var earlierAvg = earlier.Average(ld => ld.Score);
                var improvement = recentAvg - earlierAvg;

                if (improvement > 5)
                    trends.Add("Showing improvement in recent games");
                else if (improvement < -5)
                    trends.Add("Recent performance has declined slightly");
                else
                    trends.Add("Consistent performance over recent games");
            }
        }

        // Performance level assessment
        if (stats.AverageScore > 80)
            trends.Add("Advanced player with strong scoring ability");
        else if (stats.AverageScore > 60)
            trends.Add("Intermediate player with good fundamentals");
        else if (stats.AverageScore > 40)
            trends.Add("Developing player with room for improvement");
        else
            trends.Add("Beginner player building basic skills");

        // Checkout analysis
        if (stats.CheckoutPercentage > 40)
            trends.Add("Strong finishing ability");
        else if (stats.CheckoutPercentage > 20)
            trends.Add("Decent finishing, could benefit from doubles practice");
        else
            trends.Add("Finishing game needs significant improvement");

        return trends;
    }

    public async Task<HeadToHeadData> GetHeadToHeadDataAsync(string userId, string opponentName, CancellationToken cancellationToken = default)
    {
        try
        {
            var player = await _playerRepo.GetPlayerByUserIdAsync(userId);
            if (player == null)
            {
                return new HeadToHeadData { OpponentName = opponentName };
            }

            // Find opponent by name (case-insensitive)
            var allPlayers = await _playerRepo.GetPlayersAsync();
            var opponent = allPlayers.FirstOrDefault(p => 
                string.Equals(p.Name, opponentName, StringComparison.OrdinalIgnoreCase));

            if (opponent == null)
            {
                return new HeadToHeadData { OpponentName = opponentName };
            }

            // Get all matches between these two players
            var playerMatches = await _matchSummaryRepo.GetMatchesByPlayerIdAsync(player.Id);
            var headToHeadMatches = playerMatches.Where(m => 
                (m.HomePlayerId == player.Id && m.AwayPlayerId == opponent.Id) ||
                (m.HomePlayerId == opponent.Id && m.AwayPlayerId == player.Id)).ToList();

            if (!headToHeadMatches.Any())
            {
                return new HeadToHeadData { OpponentName = opponent.Name };
            }

            return CalculateHeadToHeadStats(player, opponent, headToHeadMatches);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving head-to-head data for user: {UserId} vs {OpponentName}", userId, opponentName);
            return new HeadToHeadData { OpponentName = opponentName };
        }
    }

    private HeadToHeadData CalculateHeadToHeadStats(PlayerModel player, PlayerModel opponent, List<MatchSummaryModel> matches)
    {
        var data = new HeadToHeadData
        {
            OpponentName = opponent.Name,
            TotalMatches = matches.Count
        };

        var playerWins = 0;
        var playerLegsWon = 0;
        var playerLegsLost = 0;
        var playerScores = new List<double>();
        var opponentScores = new List<double>();

        foreach (var match in matches)
        {
            bool playerIsHome = match.HomePlayerId == player.Id;
            
            if (playerIsHome)
            {
                playerLegsWon += match.HomeLegsWon;
                playerLegsLost += match.AwayLegsWon;
                playerScores.Add(match.HomeSetAverage);
                opponentScores.Add(match.AwaySetAverage);
                
                if (match.HomeLegsWon > match.AwayLegsWon)
                    playerWins++;
            }
            else
            {
                playerLegsWon += match.AwayLegsWon;
                playerLegsLost += match.HomeLegsWon;
                playerScores.Add(match.AwaySetAverage);
                opponentScores.Add(match.HomeSetAverage);
                
                if (match.AwayLegsWon > match.HomeLegsWon)
                    playerWins++;
            }
        }

        data.MatchesWon = playerWins;
        data.MatchesLost = data.TotalMatches - playerWins;
        data.WinPercentage = data.TotalMatches > 0 ? (double)playerWins / data.TotalMatches * 100 : 0;
        
        data.LegsWon = playerLegsWon;
        data.LegsLost = playerLegsLost;
        data.LegWinPercentage = (playerLegsWon + playerLegsLost) > 0 ? 
            (double)playerLegsWon / (playerLegsWon + playerLegsLost) * 100 : 0;

        data.AverageScoreVsOpponent = playerScores.Any() ? playerScores.Average() : 0;
        data.OpponentAverageScore = opponentScores.Any() ? opponentScores.Average() : 0;

        // Last match result
        var lastMatch = matches.OrderByDescending(m => m.Id).FirstOrDefault();
        if (lastMatch != null)
        {
            bool playerWonLast = (lastMatch.HomePlayerId == player.Id && lastMatch.HomeLegsWon > lastMatch.AwayLegsWon) ||
                                (lastMatch.AwayPlayerId == player.Id && lastMatch.AwayLegsWon > lastMatch.HomeLegsWon);
            data.LastMatchResult = playerWonLast ? "Won" : "Lost";
        }

        // Performance trends vs this opponent
        data.PerformanceTrends = GenerateHeadToHeadTrends(data);

        return data;
    }

    private List<string> GenerateHeadToHeadTrends(HeadToHeadData data)
    {
        var trends = new List<string>();

        if (data.WinPercentage >= 70)
            trends.Add("Dominant matchup");
        else if (data.WinPercentage >= 55)
            trends.Add("Favorable matchup");
        else if (data.WinPercentage >= 45)
            trends.Add("Even matchup");
        else
            trends.Add("Challenging opponent");

        if (data.AverageScoreVsOpponent > data.OpponentAverageScore)
            trends.Add("Outscoring opponent on average");
        else if (data.AverageScoreVsOpponent < data.OpponentAverageScore - 5)
            trends.Add("Opponent has scoring advantage");

        return trends;
    }

    public async Task<List<string>> GetOpponentListAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var player = await _playerRepo.GetPlayerByUserIdAsync(userId);
            if (player == null)
            {
                return new List<string>();
            }

            // Get all matches for this player
            var matches = await _matchSummaryRepo.GetMatchesByPlayerIdAsync(player.Id);
            
            // Get unique opponent IDs
            var opponentIds = matches.Select(m => 
                m.HomePlayerId == player.Id ? m.AwayPlayerId : m.HomePlayerId).Distinct().ToList();

            // Get opponent names
            var opponentNames = new List<string>();
            foreach (var opponentId in opponentIds)
            {
                var opponent = await _playerRepo.GetPlayerByIdAsync(opponentId);
                if (opponent != null)
                {
                    opponentNames.Add(opponent.Name);
                }
            }

            return opponentNames.OrderBy(name => name).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving opponent list for user: {UserId}", userId);
            return new List<string>();
        }
    }
} 