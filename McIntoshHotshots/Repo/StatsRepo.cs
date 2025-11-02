using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using McIntoshHotshots.Factory;
using McIntoshHotshots.Model;

namespace McIntoshHotshots.Repo;

public interface IStatsRepo
{
    // Player Statistics
    Task<PlayerStatistics?> GetPlayerStatisticsAsync(int playerId, DateTime startDate, DateTime endDate);
    Task<IEnumerable<PlayerStatistics>> GetPlayerTimeSeriesAsync(int playerId, string interval = "month");
    Task<IEnumerable<TimeSeriesDataPoint>> GetPlayerMetricTimeSeriesAsync(int playerId, string metricName, DateTime? startDate = null, DateTime? endDate = null);

    // Tournament Statistics
    Task<TournamentStatistics?> GetTournamentStatisticsAsync(int tournamentId);
    Task<IEnumerable<TournamentStatistics>> GetTournamentTimeSeriesAsync(DateTime? startDate = null, DateTime? endDate = null);

    // Head-to-Head
    Task<HeadToHeadRecord?> GetHeadToHeadRecordAsync(int player1Id, int player2Id);

    // Chart Data Helpers
    Task<ChartConfiguration> GetPlayerPerformanceChartAsync(int playerId, string metricName, DateTime? startDate = null, DateTime? endDate = null);
    Task<ChartConfiguration> GetTournamentComparisonChartAsync(List<int> tournamentIds);
    Task<ChartConfiguration> GetHeadToHeadComparisonChartAsync(int player1Id, int player2Id);
}

public class StatsRepo : IStatsRepo
{
    private readonly IDbConnectionFactory _connectionFactory;

    public StatsRepo(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<PlayerStatistics?> GetPlayerStatisticsAsync(int playerId, DateTime startDate, DateTime endDate)
    {
        using var connection = _connectionFactory.CreateConnection();

        var query = @"
            WITH player_matches AS (
                SELECT
                    ms.id,
                    ms.home_player_id,
                    ms.away_player_id,
                    ms.home_player_score,
                    ms.away_player_score,
                    ms.tournament_id,
                    t.date as tournament_date,
                    CASE
                        WHEN ms.home_player_id = @PlayerId THEN ms.home_player_score > ms.away_player_score
                        ELSE ms.away_player_score > ms.home_player_score
                    END as is_win
                FROM match_summary ms
                JOIN tournament t ON ms.tournament_id = t.id
                WHERE (ms.home_player_id = @PlayerId OR ms.away_player_id = @PlayerId)
                    AND t.date BETWEEN @StartDate AND @EndDate
            ),
            leg_stats AS (
                SELECT
                    l.match_id,
                    l.winner_id,
                    l.home_player_darts_thrown,
                    l.away_player_darts_thrown,
                    CASE
                        WHEN pm.home_player_id = @PlayerId THEN l.home_player_darts_thrown
                        ELSE l.away_player_darts_thrown
                    END as player_darts
                FROM leg l
                JOIN player_matches pm ON l.match_id = pm.id
            ),
            throw_stats_agg AS (
                SELECT
                    ROUND(AVG(ld.score)::numeric, 2) as avg_score,
                    PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY ld.score) as median_score,
                    MAX(ld.score) as max_score,
                    MIN(CASE WHEN ld.score > 0 THEN ld.score END) as min_score,
                    COUNT(CASE WHEN ld.score_remaining_before_throw <= 170 AND ld.darts_used > 0 THEN 1 END) as checkout_attempts,
                    COUNT(CASE WHEN ld.score_remaining_before_throw <= 170 THEN 1 END) as checkout_opportunities,
                    COUNT(CASE WHEN ld.score >= 40 THEN 1 END) as doubles_hit,
                    COUNT(*) as total_throws
                FROM leg_detail ld
                JOIN leg l ON ld.leg_id = l.id
                JOIN player_matches pm ON l.match_id = pm.id
                WHERE ld.player_id = @PlayerId
            )
            SELECT
                @PlayerId as player_id,
                p.name as player_name,
                @StartDate as period_start,
                @EndDate as period_end,

                -- Match Statistics
                COUNT(DISTINCT pm.id) as matches_played,
                COUNT(DISTINCT CASE WHEN pm.is_win THEN pm.id END) as matches_won,
                COUNT(DISTINCT CASE WHEN NOT pm.is_win THEN pm.id END) as matches_lost,
                CASE
                    WHEN COUNT(DISTINCT pm.id) > 0
                    THEN ROUND(CAST(COUNT(DISTINCT CASE WHEN pm.is_win THEN pm.id END) AS NUMERIC) / COUNT(DISTINCT pm.id) * 100, 2)
                    ELSE 0
                END as win_rate,

                -- Scoring Metrics
                COALESCE(tsa.avg_score, 0) as average_score,
                COALESCE(ROUND(tsa.median_score::numeric, 2), 0) as median_score,
                COALESCE(tsa.max_score, 0) as highest_score,
                COALESCE(tsa.min_score, 0) as lowest_score,

                -- Advanced Metrics
                COALESCE(ROUND(
                    CAST(tsa.checkout_attempts AS NUMERIC) /
                    NULLIF(tsa.checkout_opportunities, 0) * 100, 2
                ), 0) as checkout_percentage,

                COALESCE(ROUND(
                    CAST(tsa.doubles_hit AS NUMERIC) /
                    NULLIF(tsa.total_throws, 0) * 100, 2
                ), 0) as doubles_hit_rate,

                100.0 as completion_rate, -- Placeholder

                -- Leg Statistics
                COUNT(DISTINCT ls.match_id) as total_legs,
                COUNT(DISTINCT CASE WHEN ls.winner_id = @PlayerId THEN ls.match_id END) as legs_won,
                COALESCE(ROUND(AVG(ls.player_darts)::numeric, 2), 0) as average_darts_per_leg,

                -- Tournament Performance
                COUNT(DISTINCT pm.tournament_id) as tournaments_entered,
                NULL::numeric as average_placement, -- Will need tournament standings logic

                -- Elo (if available)
                NULL::numeric as elo_rating,
                NULL::numeric as elo_change

            FROM player_matches pm
            JOIN player p ON p.id = @PlayerId
            LEFT JOIN leg_stats ls ON ls.match_id = pm.id
            CROSS JOIN throw_stats_agg tsa
            GROUP BY p.name, tsa.avg_score, tsa.median_score, tsa.max_score, tsa.min_score,
                     tsa.checkout_attempts, tsa.checkout_opportunities, tsa.doubles_hit, tsa.total_throws;
        ";

        return await connection.QueryFirstOrDefaultAsync<PlayerStatistics>(
            query,
            new { PlayerId = playerId, StartDate = startDate, EndDate = endDate }
        );
    }

    public async Task<IEnumerable<PlayerStatistics>> GetPlayerTimeSeriesAsync(int playerId, string interval = "month")
    {
        using var connection = _connectionFactory.CreateConnection();

        var intervalClause = interval.ToLower() switch
        {
            "week" => "1 week",
            "month" => "1 month",
            "quarter" => "3 months",
            "year" => "1 year",
            _ => "1 month"
        };

        var query = @"
            WITH date_series AS (
                SELECT
                    generate_series(
                        date_trunc(@Interval, MIN(t.date)),
                        date_trunc(@Interval, MAX(t.date)),
                        @IntervalClause::interval
                    ) as period_start
                FROM tournament t
                JOIN match_summary ms ON ms.tournament_id = t.id
                WHERE ms.home_player_id = @PlayerId OR ms.away_player_id = @PlayerId
            )
            SELECT * FROM date_series;
        ";

        var periods = await connection.QueryAsync<DateTime>(
            query,
            new { PlayerId = playerId, Interval = interval, IntervalClause = intervalClause }
        );

        var results = new List<PlayerStatistics>();
        foreach (var periodStart in periods)
        {
            var periodEnd = interval.ToLower() switch
            {
                "week" => periodStart.AddDays(7).AddSeconds(-1),
                "month" => periodStart.AddMonths(1).AddSeconds(-1),
                "quarter" => periodStart.AddMonths(3).AddSeconds(-1),
                "year" => periodStart.AddYears(1).AddSeconds(-1),
                _ => periodStart.AddMonths(1).AddSeconds(-1)
            };

            var stats = await GetPlayerStatisticsAsync(playerId, periodStart, periodEnd);
            if (stats != null && stats.MatchesPlayed > 0)
            {
                results.Add(stats);
            }
        }

        return results;
    }

    public async Task<IEnumerable<TimeSeriesDataPoint>> GetPlayerMetricTimeSeriesAsync(
        int playerId,
        string metricName,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        var stats = await GetPlayerTimeSeriesAsync(playerId);

        var filteredStats = stats;
        if (startDate.HasValue)
            filteredStats = filteredStats.Where(s => s.PeriodStart >= startDate.Value);
        if (endDate.HasValue)
            filteredStats = filteredStats.Where(s => s.PeriodEnd <= endDate.Value);

        return filteredStats.Select(s => new TimeSeriesDataPoint
        {
            Date = s.PeriodStart,
            Label = s.PeriodStart.ToString("MMM yyyy"),
            Value = GetMetricValue(s, metricName),
            MetricName = metricName,
            Metadata = new Dictionary<string, object>
            {
                { "matches_played", s.MatchesPlayed },
                { "player_name", s.PlayerName }
            }
        });
    }

    public async Task<TournamentStatistics?> GetTournamentStatisticsAsync(int tournamentId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var query = @"
            WITH tournament_players AS (
                SELECT DISTINCT home_player_id as player_id
                FROM match_summary
                WHERE tournament_id = @TournamentId
                UNION
                SELECT DISTINCT away_player_id as player_id
                FROM match_summary
                WHERE tournament_id = @TournamentId
            ),
            tournament_matches AS (
                SELECT ms.id, ms.home_player_id, ms.away_player_id
                FROM match_summary ms
                WHERE ms.tournament_id = @TournamentId
            ),
            tournament_legs AS (
                SELECT l.id, l.home_player_darts_thrown, l.away_player_darts_thrown
                FROM leg l
                JOIN match_summary ms ON l.match_id = ms.id
                WHERE ms.tournament_id = @TournamentId
            ),
            throw_data_agg AS (
                SELECT
                    ROUND(AVG(ld.score)::numeric, 2) as avg_score,
                    PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY ld.score) as median_score,
                    MAX(ld.score) as max_score,
                    MIN(CASE WHEN ld.score > 0 THEN ld.score END) as min_score
                FROM leg_detail ld
                JOIN leg l ON ld.leg_id = l.id
                JOIN match_summary ms ON l.match_id = ms.id
                WHERE ms.tournament_id = @TournamentId
            )
            SELECT
                @TournamentId as tournament_id,
                t.date as tournament_date,
                'Tournament' as tournament_name, -- Update with actual name field

                -- Participation
                (SELECT COUNT(*) FROM tournament_players) as total_players,
                (SELECT COUNT(*) FROM tournament_matches) as total_matches,
                (SELECT COUNT(*) FROM tournament_legs) as total_legs,

                -- Scoring Metrics
                COALESCE(tda.avg_score, 0) as average_score,
                COALESCE(ROUND(tda.median_score::numeric, 2), 0) as median_score,
                COALESCE(tda.max_score, 0) as highest_score,
                COALESCE(tda.min_score, 0) as lowest_score,

                -- Performance Metrics (placeholders)
                0 as average_checkout_percentage,
                0 as average_doubles_hit_rate,
                100.0 as average_completion_rate,
                COALESCE((
                    SELECT ROUND(AVG((home_player_darts_thrown + away_player_darts_thrown) / 2.0)::numeric, 2)
                    FROM tournament_legs
                ), 0) as average_darts_per_leg,

                -- Winner (placeholder - needs tournament winner logic)
                NULL::int as winner_id,
                NULL::text as winner_name,

                -- Time Metrics (placeholders)
                NULL::interval as average_match_duration,
                NULL::interval as total_play_time

            FROM tournament t
            CROSS JOIN throw_data_agg tda
            WHERE t.id = @TournamentId;
        ";

        return await connection.QueryFirstOrDefaultAsync<TournamentStatistics>(
            query,
            new { TournamentId = tournamentId }
        );
    }

    public async Task<IEnumerable<TournamentStatistics>> GetTournamentTimeSeriesAsync(
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        var query = @"
            SELECT id FROM tournament
            WHERE (@StartDate IS NULL OR date >= @StartDate)
                AND (@EndDate IS NULL OR date <= @EndDate)
            ORDER BY date;
        ";

        var tournamentIds = await connection.QueryAsync<int>(
            query,
            new { StartDate = startDate, EndDate = endDate }
        );

        var results = new List<TournamentStatistics>();
        foreach (var tournamentId in tournamentIds)
        {
            var stats = await GetTournamentStatisticsAsync(tournamentId);
            if (stats != null)
            {
                results.Add(stats);
            }
        }

        return results;
    }

    public async Task<HeadToHeadRecord?> GetHeadToHeadRecordAsync(int player1Id, int player2Id)
    {
        using var connection = _connectionFactory.CreateConnection();

        var query = @"
            WITH h2h_matches AS (
                SELECT
                    ms.*,
                    t.date,
                    CASE
                        WHEN ms.home_player_id = @Player1Id AND ms.home_player_score > ms.away_player_score THEN @Player1Id
                        WHEN ms.away_player_id = @Player1Id AND ms.away_player_score > ms.home_player_score THEN @Player1Id
                        WHEN ms.home_player_id = @Player2Id AND ms.home_player_score > ms.away_player_score THEN @Player2Id
                        WHEN ms.away_player_id = @Player2Id AND ms.away_player_score > ms.home_player_score THEN @Player2Id
                        ELSE NULL
                    END as winner_id
                FROM match_summary ms
                JOIN tournament t ON ms.tournament_id = t.id
                WHERE (ms.home_player_id = @Player1Id AND ms.away_player_id = @Player2Id)
                    OR (ms.home_player_id = @Player2Id AND ms.away_player_id = @Player1Id)
            ),
            player1_throws AS (
                SELECT score FROM leg_detail
                WHERE player_id = @Player1Id
                    AND leg_id IN (SELECT l.id FROM leg l JOIN h2h_matches hm ON l.match_id = hm.id)
            ),
            player2_throws AS (
                SELECT score FROM leg_detail
                WHERE player_id = @Player2Id
                    AND leg_id IN (SELECT l.id FROM leg l JOIN h2h_matches hm ON l.match_id = hm.id)
            )
            SELECT
                @Player1Id as player1_id,
                p1.name as player1_name,
                @Player2Id as player2_id,
                p2.name as player2_name,

                -- Overall Record
                COUNT(*) as total_matches,
                SUM(CASE WHEN hm.winner_id = @Player1Id THEN 1 ELSE 0 END) as player1_wins,
                SUM(CASE WHEN hm.winner_id = @Player2Id THEN 1 ELSE 0 END) as player2_wins,
                SUM(CASE WHEN hm.winner_id IS NULL THEN 1 ELSE 0 END) as draws,

                -- Leg Statistics (placeholders)
                0 as total_legs,
                0 as player1_legs_won,
                0 as player2_legs_won,

                -- Performance Comparison
                COALESCE((SELECT ROUND(AVG(score)::numeric, 2) FROM player1_throws), 0) as player1_average_score,
                COALESCE((SELECT ROUND(AVG(score)::numeric, 2) FROM player2_throws), 0) as player2_average_score,

                -- Placeholder metrics
                0 as player1_checkout_percentage,
                0 as player2_checkout_percentage,
                0 as player1_doubles_hit_rate,
                0 as player2_doubles_hit_rate,

                -- Streak (placeholder)
                0 as current_streak,
                0 as player1_longest_win_streak,
                0 as player2_longest_win_streak,

                -- Recent/Historical
                MAX(hm.date) as last_match_date,
                (SELECT winner_id FROM h2h_matches ORDER BY date DESC LIMIT 1) as last_match_winner_id,
                MIN(hm.date) as first_match_date

            FROM h2h_matches hm
            JOIN player p1 ON p1.id = @Player1Id
            JOIN player p2 ON p2.id = @Player2Id
            GROUP BY p1.name, p2.name
            HAVING COUNT(*) > 0;
        ";

        return await connection.QueryFirstOrDefaultAsync<HeadToHeadRecord>(
            query,
            new { Player1Id = player1Id, Player2Id = player2Id }
        );
    }

    public async Task<ChartConfiguration> GetPlayerPerformanceChartAsync(
        int playerId,
        string metricName,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        var dataPoints = await GetPlayerMetricTimeSeriesAsync(playerId, metricName, startDate, endDate);

        var config = new ChartConfiguration
        {
            Type = "line",
            Labels = dataPoints.Select(d => d.Label).ToList(),
            Datasets = new List<ChartDataset>
            {
                new ChartDataset
                {
                    Label = metricName,
                    Data = dataPoints.Select(d => d.Value).ToList(),
                    BorderColor = "#4f46e5",
                    BackgroundColor = "rgba(79, 70, 229, 0.1)",
                    BorderWidth = 2,
                    Fill = true,
                    Tension = 0.4m
                }
            }
        };

        return config;
    }

    public async Task<ChartConfiguration> GetTournamentComparisonChartAsync(List<int> tournamentIds)
    {
        var allStats = new List<TournamentStatistics>();
        foreach (var id in tournamentIds)
        {
            var stats = await GetTournamentStatisticsAsync(id);
            if (stats != null)
            {
                allStats.Add(stats);
            }
        }

        var config = new ChartConfiguration
        {
            Type = "bar",
            Labels = allStats.Select(s => s.TournamentDate.ToString("MMM dd, yyyy")).ToList(),
            Datasets = new List<ChartDataset>
            {
                new ChartDataset
                {
                    Label = "Average Score",
                    Data = allStats.Select(s => s.AverageScore).ToList(),
                    BorderColor = "#4f46e5",
                    BackgroundColor = "rgba(79, 70, 229, 0.7)",
                    BorderWidth = 1
                }
            }
        };

        return config;
    }

    public async Task<ChartConfiguration> GetHeadToHeadComparisonChartAsync(int player1Id, int player2Id)
    {
        var h2h = await GetHeadToHeadRecordAsync(player1Id, player2Id);
        if (h2h == null)
        {
            return new ChartConfiguration { Type = "bar" };
        }

        var config = new ChartConfiguration
        {
            Type = "radar",
            Labels = new List<string> { "Wins", "Average Score", "Checkout %", "Doubles Hit Rate" },
            Datasets = new List<ChartDataset>
            {
                new ChartDataset
                {
                    Label = h2h.Player1Name,
                    Data = new List<decimal>
                    {
                        h2h.Player1Wins,
                        h2h.Player1AverageScore,
                        h2h.Player1CheckoutPercentage,
                        h2h.Player1DoublesHitRate
                    },
                    BorderColor = "#4f46e5",
                    BackgroundColor = "rgba(79, 70, 229, 0.2)",
                    BorderWidth = 2
                },
                new ChartDataset
                {
                    Label = h2h.Player2Name,
                    Data = new List<decimal>
                    {
                        h2h.Player2Wins,
                        h2h.Player2AverageScore,
                        h2h.Player2CheckoutPercentage,
                        h2h.Player2DoublesHitRate
                    },
                    BorderColor = "#7c3aed",
                    BackgroundColor = "rgba(124, 58, 237, 0.2)",
                    BorderWidth = 2
                }
            }
        };

        return config;
    }

    // Helper method to extract metric value from PlayerStatistics
    private decimal GetMetricValue(PlayerStatistics stats, string metricName)
    {
        return metricName.ToLower() switch
        {
            "average_score" or "averagescore" => stats.AverageScore,
            "median_score" or "medianscore" => stats.MedianScore,
            "win_rate" or "winrate" => stats.WinRate,
            "checkout_percentage" or "checkoutpercentage" => stats.CheckoutPercentage,
            "doubles_hit_rate" or "doubleshitrate" => stats.DoublesHitRate,
            "completion_rate" or "completionrate" => stats.CompletionRate,
            "average_darts_per_leg" or "averagedartsperleg" => stats.AverageDartsPerLeg,
            _ => stats.AverageScore
        };
    }
}
