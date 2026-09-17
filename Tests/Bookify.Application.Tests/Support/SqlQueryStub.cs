namespace Bookify.Application.Tests.Support
{
    using System.Data;
    using System.Data.Common;
    using Dapper;
    using Moq;
    using Moq.Protected;
    using Npgsql;

        // Runs Dapper's real parameter binding and materialization without a database server.
    internal sealed class SqlQueryStub : IDisposable
    {
        static SqlQueryStub() => SqlMapper.AddTypeHandler(new TestDateOnlyHandler());

        private readonly DataTable table = new();

        private readonly NpgsqlCommand parameterOwner = new();

        public Mock<DbConnection> Connection { get; } = new();

        public Mock<DbCommand> Command { get; } = new();

        public bool Disposed { get; private set; }

        public string Sql => Command.Object.CommandText;

        public SqlQueryStub(params (string Name, Type Type)[] columns)
        {
            foreach ((string Name, Type Type) column in columns)
            {
                table.Columns.Add(column.Name, column.Type);
            }

            Connection.SetupGet(c => c.State).Returns(ConnectionState.Open);

            Connection.Protected().Setup<DbCommand>("CreateDbCommand").Returns(Command.Object);

            Connection.Protected().Setup("Dispose", ItExpr.IsAny<bool>()).Callback<bool>(_ => Disposed = true);

            Command.SetupAllProperties();

            Command.Protected().SetupGet<DbParameterCollection>("DbParameterCollection")
                .Returns(parameterOwner.Parameters);

            Command.Protected().Setup<DbParameter>("CreateDbParameter").Returns(() => new NpgsqlParameter());

            Command.Protected().Setup<Task<DbDataReader>>(
                "ExecuteDbDataReaderAsync",
                ItExpr.IsAny<CommandBehavior>(),
                ItExpr.IsAny<CancellationToken>())
                .Returns(() => Task.FromResult<DbDataReader>(table.CreateDataReader()));
        }

        public void AddRow(params object[] values) => table.Rows.Add(values);

        public object? Parameter(string name) => parameterOwner.Parameters[name].Value;

        public IEnumerable<object?> ParameterValues => parameterOwner.Parameters.Cast<DbParameter>().Select(p => p.Value);

        public void FailWith(Exception exception) => Command.Protected()
            .Setup<Task<DbDataReader>>(
            "ExecuteDbDataReaderAsync",
            ItExpr.IsAny<CommandBehavior>(),
            ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(exception);

        public void Dispose()
        {
            table.Dispose();

            parameterOwner.Dispose();
        }

            // Application expects the host to register a DateOnly adapter for Dapper.

        private sealed class TestDateOnlyHandler : SqlMapper.TypeHandler<DateOnly>
        {
            public override DateOnly Parse(object value) => DateOnly.FromDateTime((DateTime)value);

            public override void SetValue(IDbDataParameter parameter, DateOnly value)
            {
                parameter.DbType = DbType.Date;

                parameter.Value = value;
            }
        }
    }
}
