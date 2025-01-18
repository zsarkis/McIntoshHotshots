using System.Data;
using Dapper;
using McIntoshHotshots.Factory;
using McIntoshHotshots.Model;
using Npgsql;

namespace McIntoshHotshots.Repo;

public interface IPlayerRepo
{
    Task<IEnumerable<PlayerModel>> GetPlayersAsync();
}

public class PlayerRepo : IPlayerRepo
{
    private readonly IDbConnectionFactory _connectionFactory;


    public PlayerRepo(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }
    

    public async Task<IEnumerable<PlayerModel>> GetPlayersAsync()
    {
        using var connection = _connectionFactory.CreateConnection(); // Creates connection
        var query = "SELECT * FROM player"; // Query definition
        return await connection.QueryAsync<PlayerModel>(query);
    }
}