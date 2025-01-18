using System.Data;
using Npgsql;

namespace McIntoshHotshots.Factory;


public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}


public class DbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public DbConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IDbConnection CreateConnection()
    {
        return new NpgsqlConnection(_connectionString);
    }
}