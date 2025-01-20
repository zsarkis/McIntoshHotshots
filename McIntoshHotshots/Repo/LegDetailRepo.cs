using Dapper;
using McIntoshHotshots.Factory;
using McIntoshHotshots.Model;

namespace McIntoshHotshots.Repo;

public interface ILegDetailRepo
{
    Task<LegDetailModel> GetLegDetailByIdAsync(int id);
    Task<IEnumerable<LegDetailModel>> GetLegDetailsByLegIdAsync(int legId);
    Task<int> InsertLegDetailAsync(LegDetailModel legDetail);
    Task<int> UpdateLegDetailAsync(LegDetailModel legDetail);
    Task<int> DeleteLegDetailAsync(int id);
}

public class LegDetailRepo : ILegDetailRepo
{
    private readonly IDbConnectionFactory _connectionFactory;

    public LegDetailRepo(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<LegDetailModel> GetLegDetailByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = "SELECT * FROM leg_detail WHERE id = @Id";
        return await connection.QueryFirstOrDefaultAsync<LegDetailModel>(query, new { Id = id });
    }

    public async Task<IEnumerable<LegDetailModel>> GetLegDetailsByLegIdAsync(int legId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = @"
                SELECT 
                    id AS Id,
                    match_id AS MatchId,
                    leg_id AS LegId,
                    turn_number AS TurnNumber,
                    player_id AS PlayerId,
                    score_remaining_before_throw AS ScoreRemainingBeforeThrow,
                    score AS Score,
                    darts_used AS DartsUsed
                FROM leg_detail
                WHERE leg_id = @LegId";

        return await connection.QueryAsync<LegDetailModel>(query, new { LegId = legId });
    }

    public async Task<int> InsertLegDetailAsync(LegDetailModel legDetail)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = @"
                INSERT INTO leg_detail (
                    match_id, 
                    leg_id, 
                    turn_number, 
                    player_id, 
                    score_remaining_before_throw, 
                    score, 
                    darts_used
                ) VALUES (
                    @MatchId, 
                    @LegId, 
                    @TurnNumber, 
                    @PlayerId, 
                    @ScoreRemainingBeforeThrow, 
                    @Score, 
                    @DartsUsed
                ) RETURNING id;";

        return await connection.ExecuteScalarAsync<int>(query, new
        {
            legDetail.MatchId,
            legDetail.LegId,
            legDetail.TurnNumber,
            legDetail.PlayerId,
            legDetail.ScoreRemainingBeforeThrow,
            legDetail.Score,
            legDetail.DartsUsed
        });
    }

    public async Task<int> UpdateLegDetailAsync(LegDetailModel legDetail)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = @"
                UPDATE leg_detail
                SET 
                    match_id = @MatchId, 
                    leg_id = @LegId, 
                    turn_number = @TurnNumber, 
                    player_id = @PlayerId, 
                    score_remaining_before_throw = @ScoreRemainingBeforeThrow, 
                    score = @Score, 
                    darts_used = @DartsUsed
                WHERE id = @Id";

        return await connection.ExecuteAsync(query, new
        {
            legDetail.Id,
            legDetail.MatchId,
            legDetail.LegId,
            legDetail.TurnNumber,
            legDetail.PlayerId,
            legDetail.ScoreRemainingBeforeThrow,
            legDetail.Score,
            legDetail.DartsUsed
        });
    }

    public async Task<int> DeleteLegDetailAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = "DELETE FROM leg_detail WHERE id = @Id";
        return await connection.ExecuteAsync(query, new { Id = id });
    }
}