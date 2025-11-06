using System.Data.Common;
using Api.Managers.InterfacesDao;
using API.Managers.InterfacesServices;

namespace API.DAO;

/// <summary>
/// Data Access Object for managing user profile cache entries in the database.
/// Provides methods to delete user profile cache entries by provider user identifier.
/// </summary>
public class UserProfileCacheDao(ISqlConnectionFactory factory) : IUserProfileCacheDao
{
    private readonly ISqlConnectionFactory _factory = factory ?? throw new ArgumentNullException(nameof(factory));

    public async Task DeleteByProviderUserAsync(string providerUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(providerUserId))
            throw new ArgumentException("providerUserId cannot be null or empty.", nameof(providerUserId));

        const string sql = "delete from userprofilecache where ProviderUserId = @puid";

        await using DbConnection conn = await _factory.CreateOpenAsync(ct);
        await using DbCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        DbParameter param = cmd.CreateParameter();
        param.ParameterName = "@puid";
        param.Value = providerUserId;
        cmd.Parameters.Add(param);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <inheritdoc />
    public async Task DeleteByProviderUserAsync(string providerUserId, DbConnection conn, DbTransaction tx)
    {
        if (string.IsNullOrWhiteSpace(providerUserId))
            throw new ArgumentException("providerUserId cannot be null or empty.", nameof(providerUserId));
        if (conn is null || tx is null)
            throw new ArgumentNullException(nameof(conn));

        const string sql = "delete from userprofilecache where ProviderUserId = @puid";

        await using DbCommand cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;

        DbParameter param = cmd.CreateParameter();
        param.ParameterName = "@puid";
        param.Value = providerUserId;
        cmd.Parameters.Add(param);

        await cmd.ExecuteNonQueryAsync();
    }
}