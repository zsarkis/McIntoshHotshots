using Dapper;
using McIntoshHotshots.Factory;
using McIntoshHotshots.Model;

namespace McIntoshHotshots.Repo;

public interface ILegRepo
{
    Task<LegModel> GetLegByIdAsync(int id);
    Task<IEnumerable<LegModel>> GetLegsByMatchIdAsync(int matchId);
    Task<int> InsertLegAsync(LegModel leg);
    Task<int> UpdateLegAsync(LegModel leg);
}

public class LegRepo : ILegRepo
{
    private readonly IDbConnectionFactory _connectionFactory;

    public LegRepo(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<LegModel> GetLegByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = "SELECT * FROM leg WHERE id = @Id";
        return await connection.QueryFirstOrDefaultAsync<LegModel>(query, new { Id = id });
    }

    public async Task<IEnumerable<LegModel>> GetLegsByMatchIdAsync(int matchId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = @"
                SELECT 
                    id AS Id,
                    match_id AS MatchId,
                    leg_number AS LegNumber,
                    home_player_darts_thrown AS HomePlayerDartsThrown,
                    away_player_darts_thrown AS AwayPlayerDartsThrown,
                    loser_score_remaining AS LoserScoreRemaining,
                    winner_id AS WinnerId,
                    time_elapsed AS TimeElapsed
                FROM leg
                WHERE match_id = @MatchId";

        return await connection.QueryAsync<LegModel>(query, new { MatchId = matchId });
    }

    public async Task<int> InsertLegAsync(LegModel leg)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = @"
                INSERT INTO leg (
                    match_id, 
                    leg_number, 
                    home_player_darts_thrown, 
                    away_player_darts_thrown, 
                    loser_score_remaining, 
                    winner_id, 
                    time_elapsed
                ) VALUES (
                    @MatchId, 
                    @LegNumber, 
                    @HomePlayerDartsThrown, 
                    @AwayPlayerDartsThrown, 
                    @LoserScoreRemaining, 
                    @WinnerId, 
                    @TimeElapsed
                ) RETURNING id;";

        return await connection.ExecuteAsync(query, new
        {
            leg.MatchId,
            leg.LegNumber,
            leg.HomePlayerDartsThrown,
            leg.AwayPlayerDartsThrown,
            leg.LoserScoreRemaining,
            leg.WinnerId,
            leg.TimeElapsed
        });
    }

    public async Task<int> UpdateLegAsync(LegModel leg)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = @"
                UPDATE leg
                SET 
                    match_id = @MatchId, 
                    leg_number = @LegNumber, 
                    home_player_darts_thrown = @HomePlayerDartsThrown, 
                    away_player_darts_thrown = @AwayPlayerDartsThrown, 
                    loser_score_remaining = @LoserScoreRemaining, 
                    winner_id = @WinnerId, 
                    time_elapsed = @TimeElapsed
                WHERE id = @Id";

        return await connection.ExecuteAsync(query, new
        {
            leg.Id,
            leg.MatchId,
            leg.LegNumber,
            leg.HomePlayerDartsThrown,
            leg.AwayPlayerDartsThrown,
            leg.LoserScoreRemaining,
            leg.WinnerId,
            leg.TimeElapsed
        });
    }
}