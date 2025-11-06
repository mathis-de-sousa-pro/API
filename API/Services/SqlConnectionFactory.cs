using System.Data.Common;
using API.Managers.InterfacesServices;
using MySqlConnector;

public class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;
    
    /// <summary>
    /// Initialise une nouvelle instance de <see cref="SqlConnectionFactory"/>.
    /// </summary>
    /// <param name="connectionString">Chaîne de connexion MySQL. Doit être non nulle, non vide et ne contenir que des espaces.</param>
    /// <exception cref="ArgumentException">Levée si <paramref name="connectionString"/> est null, vide ou composé uniquement d'espaces.</exception>

    public SqlConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("connectionString cannot be null or empty.", nameof(connectionString));
        _connectionString = connectionString;
    }

    public DbConnection Create() => new MySqlConnection(_connectionString);

    public async Task<DbConnection> CreateOpenAsync(CancellationToken ct = default)
    {
        var conn = Create();
        await conn.OpenAsync(ct);
        return conn;
    }
}