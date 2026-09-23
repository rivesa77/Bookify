namespace Bookify.Infrastructure.Tests.Support
{
    using System.Data;
    using System.Data.Common;
    using Bookify.Application.Abstractions.Data;
    using Bookify.Application.Abstractions.DateTimeProvider;
    using Bookify.Domain.Abstractions;
    using Bookify.Infrastructure.Outbox;
    using MediatR;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Options;
    using Moq;
    using Moq.Protected;
    using Newtonsoft.Json;
    using Npgsql;
    using Quartz;

    internal sealed class OutboxJobTestContext : IDisposable
    {
        internal const int BatchSize = 2;

        private readonly DataTable messages = new();

        private readonly NpgsqlCommand parameters = new();

        private readonly Mock<DbCommand> command = new();

        internal Mock<DbConnection> Connection { get; } = new();

        internal Mock<DbTransaction> Transaction { get; } = new();

        internal Mock<IPublisher> Publisher { get; } = new(MockBehavior.Strict);

        internal List<Dictionary<string, object?>> Updates { get; } = [];

        internal List<DbTransaction?> Transactions { get; } = [];

        internal List<string> Operations { get; } = [];

        internal bool ConnectionDisposed { get; private set; }

        internal bool TransactionDisposed { get; private set; }

        internal Exception? ReadFailure { get; set; }

        internal Exception? UpdateFailure { get; set; }

        internal string Query { get; private set; } = string.Empty;

        internal ProcessOutboxMessagesJob Job { get; }

        internal OutboxJobTestContext()
        {
            messages.Columns.Add("id", typeof(Guid));

            messages.Columns.Add("content", typeof(string));

            Connection.SetupGet(connection => connection.State).Returns(ConnectionState.Open);

            Connection.Protected().Setup<DbCommand>("CreateDbCommand").Returns(() =>
            {
                parameters.Parameters.Clear();

                return command.Object;
            });

            Connection.Protected().Setup<DbTransaction>("BeginDbTransaction", ItExpr.IsAny<IsolationLevel>())
                .Returns(Transaction.Object);

            Connection.Protected().Setup("Dispose", ItExpr.IsAny<bool>())
                .Callback<bool>(_ => ConnectionDisposed = true);

            Transaction.Protected().Setup("Dispose", ItExpr.IsAny<bool>())
                .Callback<bool>(_ => TransactionDisposed = true);

            Transaction.Setup(transaction => transaction.Commit()).Callback(() => Operations.Add("commit"));

            command.SetupAllProperties();

            command.Protected().SetupGet<DbParameterCollection>("DbParameterCollection").Returns(parameters.Parameters);

            command.Protected().Setup<DbParameter>("CreateDbParameter").Returns(() => new NpgsqlParameter());

            command.Protected().Setup<Task<DbDataReader>>(
                "ExecuteDbDataReaderAsync",
                ItExpr.IsAny<CommandBehavior>(),
                ItExpr.IsAny<CancellationToken>()).Returns(() =>
                {
                    Query = command.Object.CommandText;

                    Transactions.Add(command.Object.Transaction);

                    Operations.Add("read");

                    if (ReadFailure is not null)
                    {
                        throw ReadFailure;
                    }

                    return Task.FromResult<DbDataReader>(messages.CreateDataReader());
                });

            command.Setup(sql => sql.ExecuteNonQueryAsync(It.IsAny<CancellationToken>())).Returns(() =>
            {
                Transactions.Add(command.Object.Transaction);

                Operations.Add("update");

                if (UpdateFailure is not null)
                {
                    throw UpdateFailure;
                }

                Updates.Add(parameters.Parameters.Cast<DbParameter>().ToDictionary(
                    parameter => parameter.ParameterName,
                    parameter => parameter.Value is DBNull ? null : parameter.Value,
                    StringComparer.OrdinalIgnoreCase));

                return Task.FromResult(1);
            });

            Mock<ISqlConnectionFactory> factory = new(MockBehavior.Strict);

            factory.Setup(source => source.CreateConnection()).Returns(Connection.Object);

            Mock<IDateTimeProvider> clock = new(MockBehavior.Strict);

            clock.SetupGet(source => source.UtcNow).Returns(InfrastructureTestData.UtcNow);

            Job = new ProcessOutboxMessagesJob(
                factory.Object,
                Publisher.Object,
                clock.Object,
                Options.Create(new OutboxOptions { BatchSize = BatchSize }),
                NullLogger<ProcessOutboxMessagesJob>.Instance);
        }

        internal Guid Add(IDomainEvent domainEvent) => AddContent(JsonConvert.SerializeObject(
            domainEvent,
            new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All }));

        internal Guid AddContent(string content)
        {
            Guid id = Guid.NewGuid();

            messages.Rows.Add(id, content);

            return id;
        }

        internal Task Execute(CancellationToken token = default)
        {
            Mock<IJobExecutionContext> execution = new(MockBehavior.Strict);

            execution.SetupGet(context => context.CancellationToken).Returns(token);

            return Job.Execute(execution.Object);
        }

        public void Dispose()
        {
            messages.Dispose();

            parameters.Dispose();
        }
    }
}
