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

    public async Task<DetailedLegAnalysis> GetDetailedLegAnalysisAsync(string userId, string opponentName, CancellationToken cancellationToken = default)
    {
        try
        {
            var player = await _playerRepo.GetPlayerByUserIdAsync(userId);
            if (player == null)
            {
                return new DetailedLegAnalysis { OpponentName = opponentName };
            }

            // Find opponent by name
            var allPlayers = await _playerRepo.GetPlayersAsync();
            var opponent = allPlayers.FirstOrDefault(p => 
                string.Equals(p.Name, opponentName, StringComparison.OrdinalIgnoreCase));

            if (opponent == null)
            {
                return new DetailedLegAnalysis { OpponentName = opponentName };
            }

            // Get matches between these players
            var playerMatches = await _matchSummaryRepo.GetMatchesByPlayerIdAsync(player.Id);
            var headToHeadMatches = playerMatches.Where(m => 
                (m.HomePlayerId == player.Id && m.AwayPlayerId == opponent.Id) ||
                (m.HomePlayerId == opponent.Id && m.AwayPlayerId == player.Id)).ToList();

            if (!headToHeadMatches.Any())
            {
                return new DetailedLegAnalysis { OpponentName = opponent.Name };
            }

            // Get all leg details for the player
            var allLegDetails = await _legDetailRepo.GetLegDetailsByPlayerIdAsync(player.Id);
            
            // Filter to legs from head-to-head matches
            var matchIds = headToHeadMatches.Select(m => m.Id).ToHashSet();
            var relevantLegDetails = allLegDetails.Where(ld => 
                headToHeadMatches.Any(m => m.Id == ld.MatchId)).ToList();

            if (!relevantLegDetails.Any())
            {
                return new DetailedLegAnalysis { OpponentName = opponent.Name };
            }

            return AnalyzeDetailedLegData(opponent.Name, relevantLegDetails);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving detailed leg analysis for user: {UserId} vs {OpponentName}", userId, opponentName);
            return new DetailedLegAnalysis { OpponentName = opponentName };
        }
    }

    private DetailedLegAnalysis AnalyzeDetailedLegData(string opponentName, List<LegDetailModel> legDetails)
    {
        var analysis = new DetailedLegAnalysis
        {
            OpponentName = opponentName,
            TotalLegs = legDetails.Select(ld => ld.LegId).Distinct().Count()
        };

        if (!legDetails.Any()) return analysis;

        // Analyze checkout attempts and success rates
        var checkoutAttempts = legDetails.Where(ld => 
            ld.ScoreRemainingBeforeThrow.HasValue && 
            ld.ScoreRemainingBeforeThrow.Value <= 170 && 
            ld.ScoreRemainingBeforeThrow.Value > 0).ToList();

        var successfulCheckouts = checkoutAttempts.Where(ld => 
            ld.ScoreRemainingBeforeThrow == ld.Score).ToList();

        analysis.CheckoutSuccessRate = checkoutAttempts.Any() ? 
            (double)successfulCheckouts.Count / checkoutAttempts.Count * 100 : 0;

        // Analyze successful checkouts
        var successfulFinishes = successfulCheckouts.GroupBy(ld => ld.Score)
            .OrderByDescending(g => g.Count())
            .Take(5);

        foreach (var finish in successfulFinishes)
        {
            analysis.SuccessfulCheckouts.Add($"{finish.Key} ({finish.Count()} times)");
        }

        analysis.HighestFinish = successfulCheckouts.Any() ? successfulCheckouts.Max(ld => ld.Score) : 0;

        // Analyze missed checkout opportunities
        var missedCheckouts = checkoutAttempts.Where(ld => 
            ld.ScoreRemainingBeforeThrow != ld.Score).ToList();

        var commonMisses = missedCheckouts.GroupBy(ld => ld.ScoreRemainingBeforeThrow.Value)
            .OrderByDescending(g => g.Count())
            .Take(5);

        foreach (var miss in commonMisses)
        {
            var attempts = miss.Count();
            var avgLeft = miss.Average(ld => ld.ScoreRemainingBeforeThrow.Value - ld.Score);
            analysis.MissedCheckouts.Add($"{miss.Key} checkout (missed {attempts} times, avg {avgLeft:F1} left)");
        }

        // Analyze common scores left after throws
        var scoresLeft = legDetails.Where(ld => 
            ld.ScoreRemainingBeforeThrow.HasValue && 
            ld.ScoreRemainingBeforeThrow.Value - ld.Score > 0 &&
            ld.ScoreRemainingBeforeThrow.Value - ld.Score <= 170)
            .Select(ld => ld.ScoreRemainingBeforeThrow.Value - ld.Score)
            .GroupBy(score => score)
            .OrderByDescending(g => g.Count())
            .Take(5);

        foreach (var scoreLeft in scoresLeft)
        {
            analysis.CommonScoresLeft.Add($"{scoreLeft.Key} ({scoreLeft.Count()} times)");
        }

        // Analyze scoring patterns (high scores)
        var highScores = legDetails.Where(ld => ld.Score >= 100 && ld.DartsUsed == 3)
            .GroupBy(ld => ld.Score)
            .OrderByDescending(g => g.Count())
            .Take(3);

        foreach (var score in highScores)
        {
            analysis.ScoringPatterns.Add($"{score.Key} ({score.Count()} times)");
        }

        // Calculate average turns per leg
        var turnsPerLeg = legDetails.GroupBy(ld => ld.LegId)
            .Select(g => g.Count()).ToList();
        analysis.AverageTurnsPerLeg = turnsPerLeg.Any() ? turnsPerLeg.Average() : 0;

        // Generate specific insights
        GenerateSpecificInsights(analysis, legDetails);

        return analysis;
    }

    private void GenerateSpecificInsights(DetailedLegAnalysis analysis, List<LegDetailModel> legDetails)
    {
        // Insight: Most common finishing range
        var finishingRanges = legDetails.Where(ld => 
            ld.ScoreRemainingBeforeThrow.HasValue && 
            ld.ScoreRemainingBeforeThrow.Value <= 100)
            .Select(ld => ld.ScoreRemainingBeforeThrow.Value)
            .ToList();

        if (finishingRanges.Any())
        {
            var avgFinishingScore = finishingRanges.Average();
            analysis.SpecificInsights.Add($"You typically reach finishing range around {avgFinishingScore:F0} points");
        }

        // Insight: Consistency analysis
        var threeDartScores = legDetails.Where(ld => ld.DartsUsed == 3 && ld.Score > 0)
            .Select(ld => ld.Score).ToList();

        if (threeDartScores.Count >= 5)
        {
            var avg = threeDartScores.Average();
            var variance = threeDartScores.Select(s => Math.Pow(s - avg, 2)).Average();
            var stdDev = Math.Sqrt(variance);
            
            if (stdDev < 15)
                analysis.Strengths.Add("Very consistent scoring");
            else if (stdDev > 30)
                analysis.WeakAreas.Add("Inconsistent scoring - focus on consistency drills");
        }

        // Insight: Checkout pressure analysis
        if (analysis.CheckoutSuccessRate < 30)
        {
            analysis.WeakAreas.Add($"Low checkout success rate ({analysis.CheckoutSuccessRate:F1}%) - practice doubles regularly");
        }
        else if (analysis.CheckoutSuccessRate > 50)
        {
            analysis.Strengths.Add($"Strong finishing ability ({analysis.CheckoutSuccessRate:F1}% checkout rate)");
        }

        // Insight: Turn efficiency
        if (analysis.AverageTurnsPerLeg < 15)
        {
            analysis.Strengths.Add("Quick legs - efficient scoring");
        }
        else if (analysis.AverageTurnsPerLeg > 20)
        {
            analysis.WeakAreas.Add("Long legs - work on faster scoring to gain pressure advantage");
        }

        // Insight: Specific scoring recommendations
        var lowScores = legDetails.Where(ld => ld.DartsUsed == 3 && ld.Score < 30).Count();
        var totalThreeDartTurns = legDetails.Where(ld => ld.DartsUsed == 3).Count();
        
        if (totalThreeDartTurns > 0 && (double)lowScores / totalThreeDartTurns > 0.3)
        {
                         analysis.WeakAreas.Add("High percentage of low-scoring turns - focus on treble 20 accuracy");
         }
     }

    public async Task<FirstNineAnalysis> GetFirstNineAnalysisAsync(string userId, string? opponentName = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var player = await _playerRepo.GetPlayerByUserIdAsync(userId);
            if (player == null)
            {
                return new FirstNineAnalysis();
            }

            var analysis = new FirstNineAnalysis
            {
                OpponentName = opponentName,
                IsOpponentComparison = !string.IsNullOrEmpty(opponentName)
            };

            // Get all leg details for the player
            var allPlayerLegDetails = await _legDetailRepo.GetLegDetailsByPlayerIdAsync(player.Id);

            if (analysis.IsOpponentComparison && !string.IsNullOrEmpty(opponentName))
            {
                // Find opponent and filter to head-to-head matches
                var allPlayers = await _playerRepo.GetPlayersAsync();
                var opponent = allPlayers.FirstOrDefault(p => 
                    string.Equals(p.Name, opponentName, StringComparison.OrdinalIgnoreCase));

                if (opponent == null)
                {
                    return analysis;
                }

                // Get matches between these players
                var playerMatches = await _matchSummaryRepo.GetMatchesByPlayerIdAsync(player.Id);
                var headToHeadMatches = playerMatches.Where(m => 
                    (m.HomePlayerId == player.Id && m.AwayPlayerId == opponent.Id) ||
                    (m.HomePlayerId == opponent.Id && m.AwayPlayerId == player.Id)).ToList();

                if (!headToHeadMatches.Any())
                {
                    return analysis;
                }

                // Filter player leg details to head-to-head matches
                var matchIds = headToHeadMatches.Select(m => m.Id).ToHashSet();
                var playerH2HLegDetails = allPlayerLegDetails.Where(ld => 
                    headToHeadMatches.Any(m => m.Id == ld.MatchId)).ToList();

                // Get opponent's leg details for the same matches
                var opponentLegDetails = await _legDetailRepo.GetLegDetailsByPlayerIdAsync(opponent.Id);
                var opponentH2HLegDetails = opponentLegDetails.Where(ld => 
                    headToHeadMatches.Any(m => m.Id == ld.MatchId)).ToList();

                // Calculate first 9 averages for both players in head-to-head
                analysis.PlayerFirstNineAverages = CalculateFirstNineAverages(playerH2HLegDetails);
                analysis.OpponentFirstNineAverages = CalculateFirstNineAverages(opponentH2HLegDetails);
                
                analysis.PlayerFirstNineAverage = analysis.PlayerFirstNineAverages.Any() ? 
                    analysis.PlayerFirstNineAverages.Average() : 0;
                analysis.OpponentFirstNineAverage = analysis.OpponentFirstNineAverages.Any() ? 
                    analysis.OpponentFirstNineAverages.Average() : 0;

                analysis.TotalLegsAnalyzed = Math.Min(analysis.PlayerFirstNineAverages.Count, analysis.OpponentFirstNineAverages.Count);
                analysis.Difference = analysis.PlayerFirstNineAverage - analysis.OpponentFirstNineAverage;
                analysis.Winner = analysis.Difference > 0 ? "You" : 
                                 analysis.Difference < 0 ? opponentName : "Tied";

                GenerateFirstNineInsights(analysis);
            }
            else
            {
                // Overall first 9 analysis (no specific opponent)
                analysis.PlayerFirstNineAverages = CalculateFirstNineAverages(allPlayerLegDetails);
                analysis.PlayerFirstNineAverage = analysis.PlayerFirstNineAverages.Any() ? 
                    analysis.PlayerFirstNineAverages.Average() : 0;
                analysis.TotalLegsAnalyzed = analysis.PlayerFirstNineAverages.Count;
                
                GenerateOverallFirstNineInsights(analysis);
            }

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving first nine analysis for user: {UserId} vs {OpponentName}", userId, opponentName);
            return new FirstNineAnalysis { OpponentName = opponentName };
        }
    }

    private List<double> CalculateFirstNineAverages(List<LegDetailModel> legDetails)
    {
        var firstNineAverages = new List<double>();

        // Group by leg to calculate first 9 average for each leg
        var legGroups = legDetails.GroupBy(ld => ld.LegId);

        foreach (var legGroup in legGroups)
        {
            // Get first 3 turns (first 9 darts) for this leg
            var orderedTurns = legGroup.OrderBy(ld => ld.Id).ToList(); // Assuming Id order represents turn order
            var firstThreeTurns = orderedTurns.Take(3).ToList();

            // Only count if we have exactly 3 turns (complete first 9)
            if (firstThreeTurns.Count == 3)
            {
                var averagePerTurn = firstThreeTurns.Average(turn => turn.Score);
                firstNineAverages.Add(averagePerTurn);
            }
        }

        return firstNineAverages;
    }

    private void GenerateFirstNineInsights(FirstNineAnalysis analysis)
    {
        if (analysis.TotalLegsAnalyzed == 0) return;

        // Comparison insights
        if (Math.Abs(analysis.Difference) < 5)
        {
            analysis.Insights.Add($"Very close first 9 averages - difference of only {Math.Abs(analysis.Difference):F1} points");
        }
        else if (analysis.Difference > 10)
        {
            analysis.Insights.Add($"Strong first 9 advantage of {analysis.Difference:F1} points over {analysis.OpponentName}");
        }
        else if (analysis.Difference < -10)
        {
            analysis.Insights.Add($"Significant first 9 disadvantage of {Math.Abs(analysis.Difference):F1} points vs {analysis.OpponentName}");
        }

        // Performance level insights
        if (analysis.PlayerFirstNineAverage > 140)
        {
            analysis.Insights.Add("Excellent first 9 performance - consistently strong starts");
        }
        else if (analysis.PlayerFirstNineAverage > 120)
        {
            analysis.Insights.Add("Good first 9 average - solid opening");
        }
        else if (analysis.PlayerFirstNineAverage < 100)
        {
            analysis.Insights.Add("First 9 average needs improvement - focus on consistent opening scoring");
        }

        // Consistency insights
        if (analysis.PlayerFirstNineAverages.Any())
        {
            var variance = analysis.PlayerFirstNineAverages.Select(s => Math.Pow(s - analysis.PlayerFirstNineAverage, 2)).Average();
            var stdDev = Math.Sqrt(variance);
            
            if (stdDev < 7)
            {
                analysis.Insights.Add("Very consistent first 9 performance");
            }
            else if (stdDev > 15)
            {
                analysis.Insights.Add("Inconsistent first 9 - work on routine and consistent opening");
            }
        }
    }

    private void GenerateOverallFirstNineInsights(FirstNineAnalysis analysis)
    {
        if (analysis.PlayerFirstNineAverage > 140)
        {
            analysis.Insights.Add("Outstanding first 9 average - elite level opening");
        }
        else if (analysis.PlayerFirstNineAverage > 120)
        {
            analysis.Insights.Add("Strong first 9 average - good opening performance");
        }
        else if (analysis.PlayerFirstNineAverage > 100)
        {
            analysis.Insights.Add("Average first 9 performance - room for improvement");
        }
        else
        {
            analysis.Insights.Add("First 9 average needs significant work - focus on treble 20 accuracy");
        }

        analysis.Insights.Add($"Analyzed across {analysis.TotalLegsAnalyzed} legs");
    }

    public async Task<ScoreDownToValueAnalysis> GetScoreDownToValueAnalysisAsync(string userId, int targetValue, string? opponentName = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var player = await _playerRepo.GetPlayerByUserIdAsync(userId);
            if (player == null)
            {
                return new ScoreDownToValueAnalysis();
            }

            var analysis = new ScoreDownToValueAnalysis
            {
                TargetValue = targetValue,
                OpponentName = opponentName,
                IsOpponentComparison = !string.IsNullOrEmpty(opponentName)
            };

            // Get all leg details for the player
            var allPlayerLegDetails = await _legDetailRepo.GetLegDetailsByPlayerIdAsync(player.Id);

            if (analysis.IsOpponentComparison && !string.IsNullOrEmpty(opponentName))
            {
                // Find opponent and filter to head-to-head matches
                var allPlayers = await _playerRepo.GetPlayersAsync();
                var opponent = allPlayers.FirstOrDefault(p => 
                    string.Equals(p.Name, opponentName, StringComparison.OrdinalIgnoreCase));

                if (opponent == null)
                {
                    return analysis;
                }

                // Get matches between these players
                var playerMatches = await _matchSummaryRepo.GetMatchesByPlayerIdAsync(player.Id);
                var headToHeadMatches = playerMatches.Where(m => 
                    (m.HomePlayerId == player.Id && m.AwayPlayerId == opponent.Id) ||
                    (m.HomePlayerId == opponent.Id && m.AwayPlayerId == player.Id)).ToList();

                if (!headToHeadMatches.Any())
                {
                    return analysis;
                }

                // Filter player leg details to head-to-head matches
                var playerH2HLegDetails = allPlayerLegDetails.Where(ld => 
                    headToHeadMatches.Any(m => m.Id == ld.MatchId)).ToList();

                // Get opponent's leg details for the same matches
                var opponentLegDetails = await _legDetailRepo.GetLegDetailsByPlayerIdAsync(opponent.Id);
                var opponentH2HLegDetails = opponentLegDetails.Where(ld => 
                    headToHeadMatches.Any(m => m.Id == ld.MatchId)).ToList();

                // Calculate darts to reach target for both players
                analysis.PlayerDartCounts = CalculateDartsToReachValue(playerH2HLegDetails, targetValue);
                analysis.OpponentDartCounts = CalculateDartsToReachValue(opponentH2HLegDetails, targetValue);
                
                analysis.PlayerAverageDarts = analysis.PlayerDartCounts.Any() ? 
                    analysis.PlayerDartCounts.Average() : 0;
                analysis.OpponentAverageDarts = analysis.OpponentDartCounts.Any() ? 
                    analysis.OpponentDartCounts.Average() : 0;

                if (analysis.PlayerDartCounts.Any())
                {
                    analysis.PlayerFastestDarts = analysis.PlayerDartCounts.Min();
                    analysis.PlayerSlowestDarts = analysis.PlayerDartCounts.Max();
                }

                if (analysis.OpponentDartCounts.Any())
                {
                    analysis.OpponentFastestDarts = analysis.OpponentDartCounts.Min();
                    analysis.OpponentSlowestDarts = analysis.OpponentDartCounts.Max();
                }

                analysis.TotalLegsAnalyzed = Math.Min(analysis.PlayerDartCounts.Count, analysis.OpponentDartCounts.Count);
                analysis.Difference = analysis.PlayerAverageDarts - analysis.OpponentAverageDarts;
                analysis.Winner = analysis.Difference < 0 ? "You" : 
                                 analysis.Difference > 0 ? opponentName : "Tied";

                GenerateScoreDownToValueInsights(analysis);
            }
            else
            {
                // Overall analysis (no specific opponent)
                analysis.PlayerDartCounts = CalculateDartsToReachValue(allPlayerLegDetails, targetValue);
                analysis.PlayerAverageDarts = analysis.PlayerDartCounts.Any() ? 
                    analysis.PlayerDartCounts.Average() : 0;
                analysis.TotalLegsAnalyzed = analysis.PlayerDartCounts.Count;
                
                if (analysis.PlayerDartCounts.Any())
                {
                    analysis.PlayerFastestDarts = analysis.PlayerDartCounts.Min();
                    analysis.PlayerSlowestDarts = analysis.PlayerDartCounts.Max();
                }
                
                GenerateOverallScoreDownToValueInsights(analysis);
            }

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving score down to value analysis for user: {UserId} vs {OpponentName} target: {TargetValue}", userId, opponentName, targetValue);
            return new ScoreDownToValueAnalysis { OpponentName = opponentName, TargetValue = targetValue };
        }
    }

    private List<int> CalculateDartsToReachValue(List<LegDetailModel> legDetails, int targetValue)
    {
        var dartsToTarget = new List<int>();

        // Group by leg to analyze each leg separately
        var legGroups = legDetails.GroupBy(ld => ld.LegId);

        foreach (var legGroup in legGroups)
        {
            var orderedTurns = legGroup.OrderBy(ld => ld.Id).ToList();
            int currentScore = 501; // Standard starting score
            int dartCount = 0;

            foreach (var turn in orderedTurns)
            {
                currentScore -= turn.Score;
                dartCount += 3; // Each turn is 3 darts

                // If we've reached the target value or below, record the dart count
                if (currentScore <= targetValue)
                {
                    dartsToTarget.Add(dartCount);
                    break;
                }
            }
        }

        return dartsToTarget;
    }

    private void GenerateScoreDownToValueInsights(ScoreDownToValueAnalysis analysis)
    {
        if (analysis.TotalLegsAnalyzed == 0) return;

        var targetName = GetTargetValueName(analysis.TargetValue);

        // Comparison insights
        if (Math.Abs(analysis.Difference) < 1)
        {
            analysis.Insights.Add($"Very close pace to {targetName} - difference of only {Math.Abs(analysis.Difference):F1} darts");
        }
        else if (analysis.Difference < -3)
        {
            analysis.Insights.Add($"Faster pace to {targetName} by {Math.Abs(analysis.Difference):F1} darts vs {analysis.OpponentName}");
        }
        else if (analysis.Difference > 3)
        {
            analysis.Insights.Add($"Slower pace to {targetName} by {analysis.Difference:F1} darts vs {analysis.OpponentName}");
        }

        // Performance level insights based on common targets
        if (analysis.TargetValue == 170)
        {
            if (analysis.PlayerAverageDarts < 15)
            {
                analysis.Insights.Add("Excellent pace to double-out range - very fast scoring");
            }
            else if (analysis.PlayerAverageDarts > 21)
            {
                analysis.Insights.Add("Slow pace to double-out range - focus on higher scoring");
            }
        }
        else if (analysis.TargetValue == 40)
        {
            if (analysis.PlayerAverageDarts < 25)
            {
                analysis.Insights.Add("Fast pace to checkout range - strong finishing setup");
            }
            else if (analysis.PlayerAverageDarts > 30)
            {
                analysis.Insights.Add("Slow pace to checkout range - work on consistent scoring");
            }
        }

        // Consistency insights
        if (analysis.PlayerDartCounts.Any() && analysis.PlayerDartCounts.Count > 1)
        {
            var range = analysis.PlayerSlowestDarts - analysis.PlayerFastestDarts;
            if (range < 6)
            {
                analysis.Insights.Add($"Very consistent pace to {targetName}");
            }
            else if (range > 12)
            {
                analysis.Insights.Add($"Inconsistent pace to {targetName} - work on routine scoring");
            }
        }
    }

    private void GenerateOverallScoreDownToValueInsights(ScoreDownToValueAnalysis analysis)
    {
        var targetName = GetTargetValueName(analysis.TargetValue);

        if (analysis.TargetValue == 170)
        {
            if (analysis.PlayerAverageDarts < 15)
            {
                analysis.Insights.Add("Outstanding pace to double-out range");
            }
            else if (analysis.PlayerAverageDarts < 18)
            {
                analysis.Insights.Add("Good pace to double-out range");
            }
            else if (analysis.PlayerAverageDarts > 21)
            {
                analysis.Insights.Add("Focus on improving scoring pace to reach double-out range faster");
            }
        }
        else if (analysis.TargetValue == 40)
        {
            if (analysis.PlayerAverageDarts < 25)
            {
                analysis.Insights.Add("Excellent finishing setup - getting to checkout range quickly");
            }
            else if (analysis.PlayerAverageDarts > 30)
            {
                analysis.Insights.Add("Work on consistent scoring to reach checkout range faster");
            }
        }

        analysis.Insights.Add($"Analyzed across {analysis.TotalLegsAnalyzed} legs");
        
        if (analysis.PlayerDartCounts.Any())
        {
            analysis.Insights.Add($"Fastest to {targetName}: {analysis.PlayerFastestDarts} darts, Slowest: {analysis.PlayerSlowestDarts} darts");
        }
    }

    private string GetTargetValueName(int targetValue)
    {
        return targetValue switch
        {
            170 => "double-out range",
            40 => "checkout range",
            100 => "scoring range",
            _ => $"{targetValue} points"
        };
    }
}  