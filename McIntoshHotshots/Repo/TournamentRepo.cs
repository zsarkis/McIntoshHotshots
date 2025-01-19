using Dapper;
using McIntoshHotshots.Factory;
using McIntoshHotshots.Model;

namespace McIntoshHotshots.Repo;

public interface ITournamentRepo
{
    Task<TournamentModel> GetTournamentByIdAsync(int id);
    Task<IEnumerable<TournamentModel>> GetTournamentsAsync();
    Task<int> Insert(TournamentModel tournament);
    Task<int> UpdateTournamentAsync(TournamentModel tournament);
}

public class TournamentRepo : ITournamentRepo
{
    private readonly IDbConnectionFactory _connectionFactory;

    public TournamentRepo(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<TournamentModel> GetTournamentByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = "SELECT * FROM tournament WHERE id = @Id";
        return await connection.QueryFirstOrDefaultAsync<TournamentModel>(query, new { Id = id });
    }

    public async Task<IEnumerable<TournamentModel>> GetTournamentsAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = @"
                    SELECT 
                        id AS Id,
                        date AS Date,
                        pool_count AS PoolCount,
                        max_attendees AS MaxAttendees
                    FROM tournament";
        return await connection.QueryAsync<TournamentModel>(query);
    }

    public async Task<int> Insert(TournamentModel tournament)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = @"
        INSERT INTO tournament (date, pool_count, max_attendees)
        VALUES (@Date, @PoolCount, @MaxAttendees)
        ";
        return await connection.ExecuteAsync(query, new
        {
            tournament.Date,
            tournament.PoolCount,
            tournament.MaxAttendees
        });
    }

    public async Task<int> UpdateTournamentAsync(TournamentModel tournament)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = @"
        UPDATE tournament
        SET date = @Date, 
            pool_count = @PoolCount,
            max_attendees = @MaxAttendees
        WHERE id = @Id
        ";
        return await connection.ExecuteAsync(query, new
        {
            tournament.Id,
            tournament.Date,
            tournament.PoolCount,
            tournament.MaxAttendees
        });
    }
}