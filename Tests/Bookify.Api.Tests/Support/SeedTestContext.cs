namespace Bookify.Api.Tests.Support
{
    using System.Data;
    using Bookify.Application.Abstractions.Data;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using Npgsql;

    internal sealed class SeedTestContext : IDisposable
    {
        private readonly NpgsqlCommand parameterOwner = new();

        private readonly Mock<IDbCommand> command = new();

        private readonly ServiceProvider services;

        internal Mock<IDbConnection> Connection { get; } = new();

        internal Mock<IDbTransaction> Transaction { get; } = new();

        internal Mock<ISqlConnectionFactory> Factory { get; } = new(MockBehavior.Strict);

        internal List<string> Operations { get; } = [];

        internal List<Dictionary<string, object?>> Inserts { get; } = [];

        internal List<IDbTransaction?> CommandTransactions { get; } = [];

        internal bool HasApartments { get; set; }

        internal bool FailOnInsert { get; set; }

        internal SeedTestContext()
        {
            command.SetupProperty(c => c.CommandText, string.Empty);

            command.SetupProperty(c => c.Transaction);

            command.SetupGet(c => c.Parameters).Returns(parameterOwner.Parameters);

            command.Setup(c => c.CreateParameter()).Returns(() => new NpgsqlParameter());

            command.Setup(c => c.ExecuteScalar()).Returns(() =>
            {
                Operations.Add(command.Object.CommandText);

                CommandTransactions.Add(command.Object.Transaction);

                return HasApartments;
            });

            command.Setup(c => c.ExecuteNonQuery()).Returns(() =>
            {
                string sql = command.Object.CommandText;

                Operations.Add(sql);

                CommandTransactions.Add(command.Object.Transaction);

                if (sql.Contains("INSERT INTO", StringComparison.Ordinal))
                {
                    if (FailOnInsert)
                    {
                        throw new InvalidOperationException("Insert failed");
                    }

                    Inserts.Add(parameterOwner.Parameters.Cast<NpgsqlParameter>()
                        .ToDictionary(
                            p => p.ParameterName,
                            p => p.Value,
                            StringComparer.OrdinalIgnoreCase));
                }

                return 1;
            });

            Connection.SetupGet(c => c.State).Returns(ConnectionState.Open);

            Connection.Setup(c => c.CreateCommand()).Returns(command.Object);

            Connection.Setup(c => c.BeginTransaction()).Returns(Transaction.Object);

            Factory.Setup(f => f.CreateConnection()).Returns(Connection.Object);

            ServiceCollection registrations = new();

            registrations.AddSingleton(Factory.Object);

            services = registrations.BuildServiceProvider();
        }

        internal ApplicationBuilder CreateApp() => new(services);

        public void Dispose()
        {
            services.Dispose();

            parameterOwner.Dispose();
        }
    }
}
