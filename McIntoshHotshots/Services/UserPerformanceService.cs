using McIntoshHotshots.Model;
using McIntoshHotshots.Repo;

namespace McIntoshHotshots.Services;

public class UserPerformanceService : IUserPerformanceService
{
    private readonly IPlayerRepo _playerRepo;
    private readonly IMatchSummaryRepo _matchSummaryRepo;
    private readonly ILegDetailRepo _legDetailRepo;
    private readonly ILegRepo _legRepo;
    private readonly ILogger<UserPerformanceService> _logger;

    public UserPerformanceService(
        IPlayerRepo playerRepo,
        IMatchSummaryRepo matchSummaryRepo,
        ILegDetailRepo legDetailRepo,
        ILegRepo legRepo,
        ILogger<UserPerformanceService> logger)
    {
        _playerRepo = playerRepo;
        _matchSummaryRepo = matchSummaryRepo;
        _legDetailRepo = legDetailRepo;
        _legRepo = legRepo;
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
            var recentMatches = allMatches.OrderByDescending(m => m.Id).Take(10).ToList();

            // Get recent leg details (last 50 turns)
            var allLegDetails = await _legDetailRepo.GetLegDetailsByPlayerIdAsync(player.Id);
            var recentLegDetails = allLegDetails
                .OrderByDescending(ld => ld.Id) // Assuming Id represents chronological order
                .Take(50)
                .OrderBy(ld => ld.Id) // Restore original order if needed
                .ToList();
            
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

    public async Task<FirstNineAnalysis> GetAnyPlayerFirstNineAnalysisAsync(string playerName, CancellationToken cancellationToken = default)
    {
        try
        {
            // Find player by name
            var allPlayers = await _playerRepo.GetPlayersAsync();
            var player = allPlayers.FirstOrDefault(p => 
                string.Equals(p.Name, playerName, StringComparison.OrdinalIgnoreCase));

            if (player == null)
            {
                return new FirstNineAnalysis { OpponentName = playerName };
            }

            var analysis = new FirstNineAnalysis
            {
                OpponentName = playerName,
                IsOpponentComparison = false
            };

            // Get all leg details for this player
            var allPlayerLegDetails = await _legDetailRepo.GetLegDetailsByPlayerIdAsync(player.Id);

            // Calculate overall first 9 analysis
            analysis.PlayerFirstNineAverages = CalculateFirstNineAverages(allPlayerLegDetails);
            analysis.PlayerFirstNineAverage = analysis.PlayerFirstNineAverages.Any() ? 
                analysis.PlayerFirstNineAverages.Average() : 0;
            analysis.TotalLegsAnalyzed = analysis.PlayerFirstNineAverages.Count;
            
            GenerateOverallFirstNineInsights(analysis);

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving first nine analysis for player: {PlayerName}", playerName);
            return new FirstNineAnalysis { OpponentName = playerName };
        }
    }

    public async Task<ScoreDownToValueAnalysis> GetAnyPlayerScoreDownToValueAnalysisAsync(string playerName, int targetValue, CancellationToken cancellationToken = default)
    {
        try
        {
            // Find player by name
            var allPlayers = await _playerRepo.GetPlayersAsync();
            var player = allPlayers.FirstOrDefault(p => 
                string.Equals(p.Name, playerName, StringComparison.OrdinalIgnoreCase));

            if (player == null)
            {
                return new ScoreDownToValueAnalysis { OpponentName = playerName, TargetValue = targetValue };
            }

            var analysis = new ScoreDownToValueAnalysis
            {
                TargetValue = targetValue,
                OpponentName = playerName,
                IsOpponentComparison = false
            };

            // Get all leg details for this player
            var allPlayerLegDetails = await _legDetailRepo.GetLegDetailsByPlayerIdAsync(player.Id);

            // Calculate score down to value analysis
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

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving score down to value analysis for player: {PlayerName} target: {TargetValue}", playerName, targetValue);
            return new ScoreDownToValueAnalysis { OpponentName = playerName, TargetValue = targetValue };
        }
    }

    public async Task<UserPerformanceData> GetAnyPlayerPerformanceDataAsync(string playerName, CancellationToken cancellationToken = default)
    {
        try
        {
            // Find player by name
            var allPlayers = await _playerRepo.GetPlayersAsync();
            var player = allPlayers.FirstOrDefault(p => 
                string.Equals(p.Name, playerName, StringComparison.OrdinalIgnoreCase));

            if (player == null)
            {
                return new UserPerformanceData();
            }

            // Get all matches for this player
            var matches = await _matchSummaryRepo.GetMatchesByPlayerIdAsync(player.Id);
            
            // Get leg details for performance calculation
            var legDetails = await _legDetailRepo.GetLegDetailsByPlayerIdAsync(player.Id);

            // Calculate performance stats
            var stats = CalculatePerformanceStats(player, matches, legDetails);

            return new UserPerformanceData
            {
                Player = player,
                Stats = stats,
                RecentLegDetails = legDetails.TakeLast(10).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance data for player: {PlayerName}", playerName);
            return new UserPerformanceData();
        }
    }

    public async Task<List<string>> GetAllPlayerNamesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var allPlayers = await _playerRepo.GetPlayersAsync();
            return allPlayers.Select(p => p.Name).OrderBy(name => name).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all player names");
            return new List<string>();
        }
    }

    public async Task<AverageScorePerTurnAnalysis> GetAverageScorePerTurnDownToValueAsync(string userId, int targetValue, string? opponentName = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var player = await _playerRepo.GetPlayerByUserIdAsync(userId);
            if (player == null)
            {
                return new AverageScorePerTurnAnalysis();
            }

            var analysis = new AverageScorePerTurnAnalysis
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

                // Calculate average score per turn for both players
                analysis.PlayerScorePerTurnAverages = CalculateAverageScorePerTurnToTarget(playerH2HLegDetails, targetValue);
                analysis.OpponentScorePerTurnAverages = CalculateAverageScorePerTurnToTarget(opponentH2HLegDetails, targetValue);
                
                analysis.PlayerAverageScorePerTurn = analysis.PlayerScorePerTurnAverages.Any() ? 
                    analysis.PlayerScorePerTurnAverages.Average() : 0;
                analysis.OpponentAverageScorePerTurn = analysis.OpponentScorePerTurnAverages.Any() ? 
                    analysis.OpponentScorePerTurnAverages.Average() : 0;

                if (analysis.PlayerScorePerTurnAverages.Any())
                {
                    analysis.PlayerBestLegAverage = analysis.PlayerScorePerTurnAverages.Max();
                    analysis.PlayerWorstLegAverage = analysis.PlayerScorePerTurnAverages.Min();
                }

                if (analysis.OpponentScorePerTurnAverages.Any())
                {
                    analysis.OpponentBestLegAverage = analysis.OpponentScorePerTurnAverages.Max();
                    analysis.OpponentWorstLegAverage = analysis.OpponentScorePerTurnAverages.Min();
                }

                analysis.TotalLegsAnalyzed = Math.Min(analysis.PlayerScorePerTurnAverages.Count, analysis.OpponentScorePerTurnAverages.Count);
                analysis.Difference = analysis.PlayerAverageScorePerTurn - analysis.OpponentAverageScorePerTurn;
                analysis.Winner = analysis.Difference > 0 ? "You" : 
                                 analysis.Difference < 0 ? opponentName : "Tied";

                GenerateAverageScorePerTurnInsights(analysis);
            }
            else
            {
                // Overall analysis (no specific opponent)
                analysis.PlayerScorePerTurnAverages = CalculateAverageScorePerTurnToTarget(allPlayerLegDetails, targetValue);
                analysis.PlayerAverageScorePerTurn = analysis.PlayerScorePerTurnAverages.Any() ? 
                    analysis.PlayerScorePerTurnAverages.Average() : 0;
                analysis.TotalLegsAnalyzed = analysis.PlayerScorePerTurnAverages.Count;
                
                if (analysis.PlayerScorePerTurnAverages.Any())
                {
                    analysis.PlayerBestLegAverage = analysis.PlayerScorePerTurnAverages.Max();
                    analysis.PlayerWorstLegAverage = analysis.PlayerScorePerTurnAverages.Min();
                }
                
                GenerateOverallAverageScorePerTurnInsights(analysis);
            }

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving average score per turn analysis for user: {UserId} vs {OpponentName} target: {TargetValue}", userId, opponentName, targetValue);
            return new AverageScorePerTurnAnalysis { OpponentName = opponentName, TargetValue = targetValue };
        }
    }

    public async Task<AverageScorePerTurnAnalysis> GetAnyPlayerAverageScorePerTurnDownToValueAsync(string playerName, int targetValue, CancellationToken cancellationToken = default)
    {
        try
        {
            // Find player by name
            var allPlayers = await _playerRepo.GetPlayersAsync();
            var player = allPlayers.FirstOrDefault(p => 
                string.Equals(p.Name, playerName, StringComparison.OrdinalIgnoreCase));

            if (player == null)
            {
                return new AverageScorePerTurnAnalysis { OpponentName = playerName, TargetValue = targetValue };
            }

            var analysis = new AverageScorePerTurnAnalysis
            {
                TargetValue = targetValue,
                OpponentName = playerName,
                IsOpponentComparison = false
            };

            // Get all leg details for this player
            var allPlayerLegDetails = await _legDetailRepo.GetLegDetailsByPlayerIdAsync(player.Id);

            // Calculate average score per turn analysis
            analysis.PlayerScorePerTurnAverages = CalculateAverageScorePerTurnToTarget(allPlayerLegDetails, targetValue);
            analysis.PlayerAverageScorePerTurn = analysis.PlayerScorePerTurnAverages.Any() ? 
                analysis.PlayerScorePerTurnAverages.Average() : 0;
            analysis.TotalLegsAnalyzed = analysis.PlayerScorePerTurnAverages.Count;
            
            if (analysis.PlayerScorePerTurnAverages.Any())
            {
                analysis.PlayerBestLegAverage = analysis.PlayerScorePerTurnAverages.Max();
                analysis.PlayerWorstLegAverage = analysis.PlayerScorePerTurnAverages.Min();
            }
            
            GenerateOverallAverageScorePerTurnInsights(analysis);

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving average score per turn analysis for player: {PlayerName} target: {TargetValue}", playerName, targetValue);
            return new AverageScorePerTurnAnalysis { OpponentName = playerName, TargetValue = targetValue };
        }
    }

    private List<double> CalculateAverageScorePerTurnToTarget(List<LegDetailModel> legDetails, int targetValue)
    {
        var averageScorePerTurnPerLeg = new List<double>();

        // Group by leg to analyze each leg separately
        var legGroups = legDetails.GroupBy(ld => ld.LegId);

        foreach (var legGroup in legGroups)
        {
            var orderedTurns = legGroup.OrderBy(ld => ld.Id).ToList();
            int currentScore = 501; // Standard starting score
            var scoringTurns = new List<int>();

            foreach (var turn in orderedTurns)
            {
                currentScore -= turn.Score;
                
                // If we're still above the target, count this as a scoring turn
                if (currentScore > targetValue)
                {
                    scoringTurns.Add(turn.Score);
                }
                else
                {
                    // We've reached checkout range, stop counting scoring turns
                    break;
                }
            }

            // Calculate average score per turn for this leg's scoring phase
            if (scoringTurns.Any())
            {
                var averageForThisLeg = scoringTurns.Average();
                averageScorePerTurnPerLeg.Add(averageForThisLeg);
            }
        }

        return averageScorePerTurnPerLeg;
    }

    private void GenerateAverageScorePerTurnInsights(AverageScorePerTurnAnalysis analysis)
    {
        if (analysis.TotalLegsAnalyzed == 0) return;

        var targetName = GetTargetValueName(analysis.TargetValue);

        // Comparison insights
        if (Math.Abs(analysis.Difference) < 2)
        {
            analysis.Insights.Add($"Very close scoring to {targetName} - difference of only {Math.Abs(analysis.Difference):F1} points per turn");
        }
        else if (analysis.Difference > 5)
        {
            analysis.Insights.Add($"Higher scoring average to {targetName} by {analysis.Difference:F1} points per turn vs {analysis.OpponentName}");
        }
        else if (analysis.Difference < -5)
        {
            analysis.Insights.Add($"Lower scoring average to {targetName} by {Math.Abs(analysis.Difference):F1} points per turn vs {analysis.OpponentName}");
        }

        // Performance level insights based on common targets
        if (analysis.TargetValue == 40)
        {
            if (analysis.PlayerAverageScorePerTurn > 55)
            {
                analysis.Insights.Add("Excellent scoring efficiency to checkout range - maintaining high averages");
            }
            else if (analysis.PlayerAverageScorePerTurn < 45)
            {
                analysis.Insights.Add("Lower scoring efficiency to checkout range - focus on treble 20 accuracy");
            }
        }
        else if (analysis.TargetValue == 170)
        {
            if (analysis.PlayerAverageScorePerTurn > 60)
            {
                analysis.Insights.Add("Strong scoring to double-out range - very efficient");
            }
            else if (analysis.PlayerAverageScorePerTurn < 50)
            {
                analysis.Insights.Add("Scoring efficiency to double-out range needs improvement");
            }
        }

        // Consistency insights
        if (analysis.PlayerScorePerTurnAverages.Any() && analysis.PlayerScorePerTurnAverages.Count > 1)
        {
            var variance = analysis.PlayerScorePerTurnAverages.Select(s => Math.Pow(s - analysis.PlayerAverageScorePerTurn, 2)).Average();
            var stdDev = Math.Sqrt(variance);
            
            if (stdDev < 8)
            {
                analysis.Insights.Add($"Very consistent scoring to {targetName}");
            }
            else if (stdDev > 15)
            {
                analysis.Insights.Add($"Inconsistent scoring to {targetName} - work on routine and accuracy");
            }
        }
    }

    private void GenerateOverallAverageScorePerTurnInsights(AverageScorePerTurnAnalysis analysis)
    {
        var targetName = GetTargetValueName(analysis.TargetValue);

        if (analysis.TargetValue == 40)
        {
            if (analysis.PlayerAverageScorePerTurn > 55)
            {
                analysis.Insights.Add("Outstanding scoring efficiency to checkout range");
            }
            else if (analysis.PlayerAverageScorePerTurn > 50)
            {
                analysis.Insights.Add("Good scoring efficiency to checkout range");
            }
            else if (analysis.PlayerAverageScorePerTurn < 45)
            {
                analysis.Insights.Add("Focus on improving scoring consistency to reach checkout range more efficiently");
            }
        }
        else if (analysis.TargetValue == 170)
        {
            if (analysis.PlayerAverageScorePerTurn > 60)
            {
                analysis.Insights.Add("Excellent scoring efficiency to double-out range");
            }
            else if (analysis.PlayerAverageScorePerTurn < 50)
            {
                analysis.Insights.Add("Work on maintaining higher scoring averages to reach double-out range");
            }
        }

        analysis.Insights.Add($"Analyzed across {analysis.TotalLegsAnalyzed} legs");
        
        if (analysis.PlayerScorePerTurnAverages.Any())
        {
            analysis.Insights.Add($"Best leg average to {targetName}: {analysis.PlayerBestLegAverage:F1}, Worst: {analysis.PlayerWorstLegAverage:F1}");
        }
    }

    public async Task<ScoreDownToValueAnalysis> GetDartsToWinFromValueAnalysisAsync(string userId, int startingValue, string? opponentName = null, CancellationToken cancellationToken = default)
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
                TargetValue = startingValue,
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

                // Calculate darts to win from value for both players
                analysis.PlayerDartCounts = CalculateDartsToWinFromValue(playerH2HLegDetails, startingValue);
                analysis.OpponentDartCounts = CalculateDartsToWinFromValue(opponentH2HLegDetails, startingValue);
                
                if (analysis.PlayerDartCounts.Any())
                {
                    analysis.PlayerAverageDarts = analysis.PlayerDartCounts.Average();
                    analysis.PlayerFastestDarts = analysis.PlayerDartCounts.Min();
                    analysis.PlayerSlowestDarts = analysis.PlayerDartCounts.Max();
                }

                if (analysis.OpponentDartCounts.Any())
                {
                    analysis.OpponentAverageDarts = analysis.OpponentDartCounts.Average();
                    analysis.OpponentFastestDarts = analysis.OpponentDartCounts.Min();
                    analysis.OpponentSlowestDarts = analysis.OpponentDartCounts.Max();
                }

                analysis.TotalLegsAnalyzed = Math.Min(analysis.PlayerDartCounts.Count, analysis.OpponentDartCounts.Count);
                analysis.Difference = analysis.PlayerAverageDarts - analysis.OpponentAverageDarts;
                analysis.Winner = analysis.Difference < 0 ? "You" : 
                                 analysis.Difference > 0 ? opponentName : "Tied";

                GenerateDartsToWinFromValueInsights(analysis);
            }
            else
            {
                // Overall analysis (no specific opponent)
                analysis.PlayerDartCounts = CalculateDartsToWinFromValue(allPlayerLegDetails, startingValue);
                if (analysis.PlayerDartCounts.Any())
                {
                    analysis.PlayerAverageDarts = analysis.PlayerDartCounts.Average();
                    analysis.PlayerFastestDarts = analysis.PlayerDartCounts.Min();
                    analysis.PlayerSlowestDarts = analysis.PlayerDartCounts.Max();
                }
                analysis.TotalLegsAnalyzed = analysis.PlayerDartCounts.Count;
                
                GenerateOverallDartsToWinFromValueInsights(analysis);
            }

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving darts to win from value analysis for user: {UserId} vs {OpponentName} starting: {StartingValue}", userId, opponentName, startingValue);
            return new ScoreDownToValueAnalysis { OpponentName = opponentName, TargetValue = startingValue };
        }
    }

    public async Task<ScoreDownToValueAnalysis> GetAnyPlayerDartsToWinFromValueAnalysisAsync(string playerName, int startingValue, CancellationToken cancellationToken = default)
    {
        try
        {
            // Find player by name
            var allPlayers = await _playerRepo.GetPlayersAsync();
            var player = allPlayers.FirstOrDefault(p => 
                string.Equals(p.Name, playerName, StringComparison.OrdinalIgnoreCase));

            if (player == null)
            {
                return new ScoreDownToValueAnalysis { OpponentName = playerName, TargetValue = startingValue };
            }

            var analysis = new ScoreDownToValueAnalysis
            {
                TargetValue = startingValue,
                OpponentName = playerName,
                IsOpponentComparison = false
            };

            // Get all leg details for this player
            var allPlayerLegDetails = await _legDetailRepo.GetLegDetailsByPlayerIdAsync(player.Id);

            // Calculate darts to win from value analysis
            analysis.PlayerDartCounts = CalculateDartsToWinFromValue(allPlayerLegDetails, startingValue);
            if (analysis.PlayerDartCounts.Any())
            {
                analysis.PlayerAverageDarts = analysis.PlayerDartCounts.Average();
                analysis.PlayerFastestDarts = analysis.PlayerDartCounts.Min();
                analysis.PlayerSlowestDarts = analysis.PlayerDartCounts.Max();
            }
            analysis.TotalLegsAnalyzed = analysis.PlayerDartCounts.Count;
            
            GenerateOverallDartsToWinFromValueInsights(analysis);

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving darts to win from value analysis for player: {PlayerName} starting: {StartingValue}", playerName, startingValue);
            return new ScoreDownToValueAnalysis { OpponentName = playerName, TargetValue = startingValue };
        }
    }

    private List<int> CalculateDartsToWinFromValue(List<LegDetailModel> legDetails, int startingValue)
    {
        var dartsToWinFromValue = new List<int>();

        // Group by leg to analyze each leg separately
        var legGroups = legDetails.GroupBy(ld => ld.LegId);

        foreach (var legGroup in legGroups)
        {
            var orderedTurns = legGroup.OrderBy(ld => ld.Id).ToList();
            int currentScore = 501; // Standard starting score
            bool reachedStartingValue = false;
            int dartsFromStartingValue = 0;
            bool wonFromStartingValue = false;

            foreach (var turn in orderedTurns)
            {
                currentScore -= turn.Score;
                
                // Check if we've reached the starting value
                if (!reachedStartingValue && currentScore <= startingValue)
                {
                    reachedStartingValue = true;
                    // Add the darts from this turn (we just reached or passed the starting value)
                    dartsFromStartingValue += turn.DartsUsed;
                }
                else if (reachedStartingValue)
                {
                    // We're now counting darts from the starting value
                    dartsFromStartingValue += turn.DartsUsed;
                }
                
                // Check if we won the leg
                if (currentScore == 0)
                {
                    wonFromStartingValue = true;
                    break;
                }
            }

            // Only count legs where we reached the starting value AND won the leg
            if (reachedStartingValue && wonFromStartingValue && dartsFromStartingValue > 0)
            {
                dartsToWinFromValue.Add(dartsFromStartingValue);
            }
        }

        return dartsToWinFromValue;
    }

    private void GenerateDartsToWinFromValueInsights(ScoreDownToValueAnalysis analysis)
    {
        if (analysis.TotalLegsAnalyzed == 0) return;

        var startingName = GetTargetValueName(analysis.TargetValue);

        // Comparison insights
        if (Math.Abs(analysis.Difference) < 1)
        {
            analysis.Insights.Add($"Very close finishing from {startingName} - difference of only {Math.Abs(analysis.Difference):F1} darts");
        }
        else if (analysis.Difference < -2)
        {
            analysis.Insights.Add($"Faster finishing from {startingName} by {Math.Abs(analysis.Difference):F1} darts vs {analysis.OpponentName}");
        }
        else if (analysis.Difference > 2)
        {
            analysis.Insights.Add($"Slower finishing from {startingName} by {analysis.Difference:F1} darts vs {analysis.OpponentName}");
        }

        // Performance level insights based on common starting values
        if (analysis.TargetValue == 170)
        {
            if (analysis.PlayerAverageDarts < 6)
            {
                analysis.Insights.Add("Excellent finishing from double-out range - very efficient");
            }
            else if (analysis.PlayerAverageDarts > 10)
            {
                analysis.Insights.Add("Finishing from double-out range needs improvement - work on double accuracy");
            }
        }
        else if (analysis.TargetValue == 100)
        {
            if (analysis.PlayerAverageDarts < 4)
            {
                analysis.Insights.Add("Strong finishing from 100 - good checkout skills");
            }
            else if (analysis.PlayerAverageDarts > 7)
            {
                analysis.Insights.Add("Work on finishing from 100 - practice common checkout combinations");
            }
        }

        // Consistency insights
        if (analysis.PlayerDartCounts.Any() && analysis.PlayerDartCounts.Count > 1)
        {
            var variance = analysis.PlayerDartCounts.Select(d => Math.Pow(d - analysis.PlayerAverageDarts, 2)).Average();
            var stdDev = Math.Sqrt(variance);
            
            if (stdDev < 2)
            {
                analysis.Insights.Add($"Very consistent finishing from {startingName}");
            }
            else if (stdDev > 4)
            {
                analysis.Insights.Add($"Inconsistent finishing from {startingName} - work on routine and composure");
            }
        }
    }

    private void GenerateOverallDartsToWinFromValueInsights(ScoreDownToValueAnalysis analysis)
    {
        var startingName = GetTargetValueName(analysis.TargetValue);

        if (analysis.TargetValue == 170)
        {
            if (analysis.PlayerAverageDarts < 6)
            {
                analysis.Insights.Add("Outstanding finishing from double-out range");
            }
            else if (analysis.PlayerAverageDarts > 10)
            {
                analysis.Insights.Add("Focus on improving finishing consistency from double-out range");
            }
        }
        else if (analysis.TargetValue == 100)
        {
            if (analysis.PlayerAverageDarts < 4)
            {
                analysis.Insights.Add("Excellent finishing from 100");
            }
            else if (analysis.PlayerAverageDarts > 7)
            {
                analysis.Insights.Add("Work on finishing combinations from 100");
            }
        }

        analysis.Insights.Add($"Analyzed across {analysis.TotalLegsAnalyzed} legs where you reached {startingName} and won");
        
        if (analysis.PlayerDartCounts.Any())
        {
            analysis.Insights.Add($"Best finish from {startingName}: {analysis.PlayerFastestDarts} darts, Worst: {analysis.PlayerSlowestDarts} darts");
        }
    }

    public async Task<FinishingAttemptsAnalysis> GetFinishingAttemptsFromValueAnalysisAsync(string userId, int startingValue, string? opponentName = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var player = await _playerRepo.GetPlayerByUserIdAsync(userId);
            if (player == null)
            {
                return new FinishingAttemptsAnalysis();
            }

            var analysis = new FinishingAttemptsAnalysis
            {
                StartingValue = startingValue,
                OpponentName = opponentName,
                IsOpponentComparison = !string.IsNullOrEmpty(opponentName)
            };

            // Get all leg details for the player
            var allPlayerLegDetails = await _legDetailRepo.GetLegDetailsByPlayerIdAsync(player.Id);
            var allLegs = await _legRepo.GetLegsAsync();

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

                // Filter to head-to-head legs
                var h2hLegs = allLegs.Where(leg => headToHeadMatches.Any(m => m.Id == leg.MatchId)).ToList();
                var playerH2HLegDetails = allPlayerLegDetails.Where(ld => 
                    h2hLegs.Any(leg => leg.Id == ld.LegId)).ToList();

                // Get opponent's leg details for the same legs
                var opponentLegDetails = await _legDetailRepo.GetLegDetailsByPlayerIdAsync(opponent.Id);
                var opponentH2HLegDetails = opponentLegDetails.Where(ld => 
                    h2hLegs.Any(leg => leg.Id == ld.LegId)).ToList();

                // Calculate finishing attempts for both players
                CalculateFinishingAttempts(analysis, playerH2HLegDetails, h2hLegs, player.Id, startingValue, "player");
                CalculateFinishingAttempts(analysis, opponentH2HLegDetails, h2hLegs, opponent.Id, startingValue, "opponent");

                GenerateFinishingAttemptsInsights(analysis, true);
            }
            else
            {
                // Overall analysis (no specific opponent)
                CalculateFinishingAttempts(analysis, allPlayerLegDetails, allLegs, player.Id, startingValue, "player");
                GenerateFinishingAttemptsInsights(analysis, false);
            }

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving finishing attempts analysis for user: {UserId} vs {OpponentName} starting: {StartingValue}", userId, opponentName, startingValue);
            return new FinishingAttemptsAnalysis { OpponentName = opponentName, StartingValue = startingValue };
        }
    }

    public async Task<FinishingAttemptsAnalysis> GetAnyPlayerFinishingAttemptsFromValueAnalysisAsync(string playerName, int startingValue, CancellationToken cancellationToken = default)
    {
        try
        {
            // Find player by name
            var allPlayers = await _playerRepo.GetPlayersAsync();
            var player = allPlayers.FirstOrDefault(p => 
                string.Equals(p.Name, playerName, StringComparison.OrdinalIgnoreCase));

            if (player == null)
            {
                return new FinishingAttemptsAnalysis { OpponentName = playerName, StartingValue = startingValue };
            }

            var analysis = new FinishingAttemptsAnalysis
            {
                StartingValue = startingValue,
                OpponentName = playerName,
                IsOpponentComparison = false
            };

            // Get all leg details and legs for this player
            var allPlayerLegDetails = await _legDetailRepo.GetLegDetailsByPlayerIdAsync(player.Id);
            var allLegs = await _legRepo.GetLegsAsync();

            // Calculate finishing attempts
            CalculateFinishingAttempts(analysis, allPlayerLegDetails, allLegs, player.Id, startingValue, "player");
            GenerateFinishingAttemptsInsights(analysis, false);

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving finishing attempts analysis for player: {PlayerName} starting: {StartingValue}", playerName, startingValue);
            return new FinishingAttemptsAnalysis { OpponentName = playerName, StartingValue = startingValue };
        }
    }

    private void CalculateFinishingAttempts(FinishingAttemptsAnalysis analysis, List<LegDetailModel> legDetails, List<LegModel> allLegs, int playerId, int startingValue, string playerType)
    {
        var winningAttempts = new List<int>();
        var losingAttempts = new List<int>();

        // Group by leg to analyze each leg separately
        var legGroups = legDetails.GroupBy(ld => ld.LegId);

        foreach (var legGroup in legGroups)
        {
            var legId = legGroup.Key;
            var leg = allLegs.FirstOrDefault(l => l.Id == legId);
            if (leg == null) continue;

            var orderedTurns = legGroup.OrderBy(ld => ld.Id).ToList();
            int currentScore = 501; // Standard starting score
            bool reachedStartingValue = false;
            int dartsFromStartingValue = 0;
            bool playerWonLeg = leg.WinnerId == playerId;

            foreach (var turn in orderedTurns)
            {
                currentScore -= turn.Score;
                
                // Check if we've reached the starting value
                if (!reachedStartingValue && currentScore <= startingValue)
                {
                    reachedStartingValue = true;
                    // Add the darts from this turn (we just reached or passed the starting value)
                    dartsFromStartingValue += turn.DartsUsed;
                }
                else if (reachedStartingValue)
                {
                    // We're now counting darts from the starting value
                    dartsFromStartingValue += turn.DartsUsed;
                }
                
                // Check if player finished the leg
                if (currentScore == 0)
                {
                    break;
                }
            }

            // Only count legs where we reached the starting value
            if (reachedStartingValue && dartsFromStartingValue > 0)
            {
                if (playerWonLeg)
                {
                    winningAttempts.Add(dartsFromStartingValue);
                }
                else
                {
                    losingAttempts.Add(dartsFromStartingValue);
                }
            }
        }

        // Set the appropriate properties based on player type
        if (playerType == "player")
        {
            analysis.WinningAttemptDarts = winningAttempts;
            analysis.LosingAttemptDarts = losingAttempts;
            analysis.SuccessfulFinishes = winningAttempts.Count;
            analysis.FailedAttempts = losingAttempts.Count;
            analysis.TotalAttempts = winningAttempts.Count + losingAttempts.Count;
            
            if (analysis.TotalAttempts > 0)
            {
                analysis.SuccessRate = (double)analysis.SuccessfulFinishes / analysis.TotalAttempts * 100;
            }
            
            if (winningAttempts.Any())
            {
                analysis.AverageDartsInWins = winningAttempts.Average();
                analysis.FastestSuccessfulFinish = winningAttempts.Min();
                analysis.SlowestSuccessfulFinish = winningAttempts.Max();
            }
            
            if (losingAttempts.Any())
            {
                analysis.AverageDartsInLosses = losingAttempts.Average();
                analysis.AverageDartsInFailedAttempts = losingAttempts.Average();
            }
        }
        else // opponent
        {
            analysis.OpponentSuccessfulFinishes = winningAttempts.Count;
            analysis.OpponentTotalAttempts = winningAttempts.Count + losingAttempts.Count;
            
            if (analysis.OpponentTotalAttempts > 0)
            {
                analysis.OpponentSuccessRate = (double)analysis.OpponentSuccessfulFinishes / analysis.OpponentTotalAttempts * 100;
            }
            
            if (winningAttempts.Any())
            {
                analysis.OpponentAverageDartsInWins = winningAttempts.Average();
            }
            
            if (losingAttempts.Any())
            {
                analysis.OpponentAverageDartsInLosses = losingAttempts.Average();
            }
        }
    }

    private void GenerateFinishingAttemptsInsights(FinishingAttemptsAnalysis analysis, bool isComparison)
    {
        if (analysis.TotalAttempts == 0) return;

        var startingName = GetTargetValueName(analysis.StartingValue);

        // Success rate insights
        if (analysis.SuccessRate > 80)
        {
            analysis.Insights.Add($"Excellent finishing rate from {startingName} ({analysis.SuccessRate:F1}%)");
        }
        else if (analysis.SuccessRate > 60)
        {
            analysis.Insights.Add($"Good finishing rate from {startingName} ({analysis.SuccessRate:F1}%)");
        }
        else if (analysis.SuccessRate > 40)
        {
            analysis.Insights.Add($"Average finishing rate from {startingName} ({analysis.SuccessRate:F1}%) - room for improvement");
        }
        else
        {
            analysis.Insights.Add($"Low finishing rate from {startingName} ({analysis.SuccessRate:F1}%) - focus on checkout practice");
        }

        // Pressure performance insights
        if (analysis.AverageDartsInLosses > 0 && analysis.AverageDartsInWins > 0)
        {
            var pressureDifference = analysis.AverageDartsInLosses - analysis.AverageDartsInWins;
            if (pressureDifference > 3)
            {
                analysis.Insights.Add($"Under pressure, you take {pressureDifference:F1} more darts on average - work on composure");
            }
            else if (pressureDifference < 1)
            {
                analysis.Insights.Add("Consistent finishing performance in both wins and losses - good mental strength");
            }
            else
            {
                analysis.Insights.Add($"Slightly slower finishing when losing ({pressureDifference:F1} more darts)");
            }
        }

        // Overall performance summary
        analysis.Insights.Add($"From {startingName}: {analysis.SuccessfulFinishes} successful finishes, {analysis.FailedAttempts} failed attempts");
        
        if (analysis.WinningAttemptDarts.Any())
        {
            analysis.Insights.Add($"Best finish: {analysis.FastestSuccessfulFinish} darts, Worst: {analysis.SlowestSuccessfulFinish} darts");
        }

        if (isComparison && analysis.OpponentTotalAttempts > 0)
        {
            var successRateDiff = analysis.SuccessRate - analysis.OpponentSuccessRate;
            if (Math.Abs(successRateDiff) < 5)
            {
                analysis.Insights.Add($"Similar finishing rates vs {analysis.OpponentName} from {startingName}");
            }
            else if (successRateDiff > 0)
            {
                analysis.Insights.Add($"Better finishing rate from {startingName} (+{successRateDiff:F1}%) vs {analysis.OpponentName}");
            }
            else
            {
                analysis.Insights.Add($"Lower finishing rate from {startingName} ({successRateDiff:F1}%) vs {analysis.OpponentName}");
            }
        }
    }
}  