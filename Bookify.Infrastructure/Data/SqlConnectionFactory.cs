namespace Bookify.Infrastructure.Data
{
    using System.Data;
    using Bookify.Application.Abstractions.Data;
    using Npgsql;

    internal sealed class SqlConnectionFactory : ISqlConnectionFactory
    {
        private readonly string connectionString;

        public SqlConnectionFactory(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public IDbConnection CreateConnection()
        {
            NpgsqlConnection connection = new(connectionString);
            connection.Open();

            return connection;
        }
    }
}