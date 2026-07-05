using Npgsql;

namespace MPM.Core.Data;

public class DbConnectionFactory(string connectionString)
{
    public NpgsqlConnection Create()
        => new(connectionString);
}
