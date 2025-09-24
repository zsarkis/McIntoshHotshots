using System.Data;
using Dapper;
using McIntoshHotshots.Factory;
using McIntoshHotshots.Model;
using Npgsql;

namespace McIntoshHotshots.Repo;

public interface IPlayerRepo
{
    Task<PlayerModel> GetPlayerByIdAsync(int id);
    Task<PlayerModel> GetPlayerByUserIdAsync(string id);
    Task<IEnumerable<PlayerModel>> GetPlayersAsync();
    Task<int> InsertPlayerAsync(PlayerModel player);
    Task<int> UpdatePlayerAsync(PlayerModel player);
}

public class PlayerRepo : IPlayerRepo
{
    private readonly IDbConnectionFactory _connectionFactory;


    public PlayerRepo(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<PlayerModel> GetPlayerByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = "SELECT * FROM player WHERE id = @Id";
        return await connection.QueryFirstOrDefaultAsync<PlayerModel>(query, new { Id = id });
    }
    
    public async Task<PlayerModel> GetPlayerByUserIdAsync(string id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = "SELECT * FROM player WHERE user_id = @Id";
        return await connection.QueryFirstOrDefaultAsync<PlayerModel>(query, new { Id = id });
    }
    
    public async Task<IEnumerable<PlayerModel>> GetPlayersAsync()
    {
        using var connection = _connectionFactory.CreateConnection(); // Creates connection
        var query = "SELECT * FROM player"; // Query definition
        return await connection.QueryAsync<PlayerModel>(query);
    }

    public async Task<int> InsertPlayerAsync(PlayerModel player)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = @"
        INSERT INTO player (name, earnings, elo_number, preferences, user_id)
        VALUES (@Name, @Earnings, @EloNumber, @Preferences::jsonb, @UserId)
    ";
        return await connection.ExecuteAsync(query, new
        {
            player.Name,
            player.Earnings,
            player.EloNumber,
            player.Preferences, // Ensure this is serialized to JSON
            player.UserId
        });
    }

    public async Task<int> UpdatePlayerAsync(PlayerModel player)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = @"
        UPDATE player
        SET name = @Name,
            earnings = @Earnings,
            elo_number = @EloNumber,
            preferences = @Preferences::jsonb,
            user_id = @UserId
        WHERE id = @Id
    ";
        return await connection.ExecuteAsync(query, new
        {
            player.Id,
            player.Name,
            player.Earnings,
            player.EloNumber,
            player.Preferences, // Ensure this is serialized to JSON
            player.UserId
        });
    }

}