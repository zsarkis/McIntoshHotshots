using Dapper;
using McIntoshHotshots.Factory;
using McIntoshHotshots.Model;

namespace McIntoshHotshots.Repo;

public interface IMatchSummaryRepo
{
    Task<MatchSummaryModel> GetMatchSummaryByIdAsync(int id);
    Task<IEnumerable<MatchSummaryModel>> GetMatchSummariesAsync();
    Task<int> InsertMatchSummaryAsync(MatchSummaryModel matchSummary);
    Task<MatchSummaryModel> CreateMatchSummaryAsync(MatchSummaryModel matchSummary);
    Task<int> UpdateMatchSummaryAsync(MatchSummaryModel matchSummary);
    Task<List<MatchSummaryModel>> GetMatchesByPlayerIdAsync(int playerId);
}

public class MatchSummaryRepo : IMatchSummaryRepo
{
    private readonly IDbConnectionFactory _connectionFactory;

    public MatchSummaryRepo(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<MatchSummaryModel> GetMatchSummaryByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = @"
        SELECT 
            id AS Id,
            url_to_recap AS UrlToRecap,
            home_player_id AS HomePlayerId,
            away_player_id AS AwayPlayerId,
            home_set_average AS HomeSetAverage,
            away_set_average AS AwaySetAverage,
            home_legs_won AS HomeLegsWon,
            away_legs_won AS AwayLegsWon,
            time_elapsed AS TimeElapsed,
            cork_winner_player_id AS CorkWinnerPlayerId,
            tournament_id AS TournamentId
        FROM match_summary 
        WHERE id = @Id";

        return await connection.QueryFirstOrDefaultAsync<MatchSummaryModel>(query, new { Id = id });
    }


    public async Task<IEnumerable<MatchSummaryModel>> GetMatchSummariesAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = @"
                SELECT 
                    id AS Id,
                    url_to_recap AS UrlToRecap,
                    home_player_id AS HomePlayerId,
                    away_player_id AS AwayPlayerId,
                    home_set_average AS HomeSetAverage,
                    away_set_average AS AwaySetAverage,
                    home_legs_won AS HomeLegsWon,
                    away_legs_won AS AwayLegsWon,
                    time_elapsed AS TimeElapsed,
                    cork_winner_player_id AS CorkWinnerPlayerId,
                    tournament_id AS TournamentId
                FROM match_summary";

        return await connection.QueryAsync<MatchSummaryModel>(query);
    }

    public async Task<int> InsertMatchSummaryAsync(MatchSummaryModel matchSummary)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = @"
        INSERT INTO match_summary (
            url_to_recap, 
            home_player_id, 
            away_player_id, 
            home_set_average, 
            away_set_average, 
            home_legs_won, 
            away_legs_won, 
            time_elapsed, 
            cork_winner_player_id,
            tournament_id
        ) VALUES (
            @UrlToRecap, 
            @HomePlayerId, 
            @AwayPlayerId, 
            @HomeSetAverage, 
            @AwaySetAverage, 
            @HomeLegsWon, 
            @AwayLegsWon, 
            @TimeElapsed, 
            @CorkWinnerPlayerId,
            @TournamentId
        ) RETURNING id;";  // Return the generated ID

        return await connection.ExecuteScalarAsync<int>(query, new
        {
            matchSummary.UrlToRecap,
            matchSummary.HomePlayerId,
            matchSummary.AwayPlayerId,
            matchSummary.HomeSetAverage,
            matchSummary.AwaySetAverage,
            matchSummary.HomeLegsWon,
            matchSummary.AwayLegsWon,
            matchSummary.TimeElapsed,
            matchSummary.CorkWinnerPlayerId,
            matchSummary.TournamentId
        });
    }

    public async Task<MatchSummaryModel> CreateMatchSummaryAsync(MatchSummaryModel matchSummary)
    {
        var id = await InsertMatchSummaryAsync(matchSummary);
        matchSummary.Id = id;
        return matchSummary;
    }

    public async Task<int> UpdateMatchSummaryAsync(MatchSummaryModel matchSummary)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = @"
                UPDATE match_summary
                SET 
                    url_to_recap = @UrlToRecap, 
                    home_player_id = @HomePlayerId, 
                    away_player_id = @AwayPlayerId, 
                    home_set_average = @HomeSetAverage, 
                    away_set_average = @AwaySetAverage, 
                    home_legs_won = @HomeLegsWon, 
                    away_legs_won = @AwayLegsWon, 
                    time_elapsed = @TimeElapsed, 
                    cork_winner_player_id = @CorkWinnerPlayerId,
                    tournament_id = @TournamentId
                WHERE id = @Id";

        return await connection.ExecuteAsync(query, new
        {
            matchSummary.Id,
            matchSummary.UrlToRecap,
            matchSummary.HomePlayerId,
            matchSummary.AwayPlayerId,
            matchSummary.HomeSetAverage,
            matchSummary.AwaySetAverage,
            matchSummary.HomeLegsWon,
            matchSummary.AwayLegsWon,
            matchSummary.TimeElapsed,
            matchSummary.CorkWinnerPlayerId,
            matchSummary.TournamentId
        });
    }

    public async Task<List<MatchSummaryModel>> GetMatchesByPlayerIdAsync(int playerId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = @"
        SELECT 
            id AS Id,
            url_to_recap AS UrlToRecap,
            home_player_id AS HomePlayerId,
            away_player_id AS AwayPlayerId,
            home_set_average AS HomeSetAverage,
            away_set_average AS AwaySetAverage,
            home_legs_won AS HomeLegsWon,
            away_legs_won AS AwayLegsWon,
            time_elapsed AS TimeElapsed,
            cork_winner_player_id AS CorkWinnerPlayerId,
            tournament_id AS TournamentId
        FROM match_summary 
        WHERE home_player_id = @PlayerId OR away_player_id = @PlayerId
        ORDER BY id DESC";

        var results = await connection.QueryAsync<MatchSummaryModel>(query, new { PlayerId = playerId });
        return results.ToList();
    }
}