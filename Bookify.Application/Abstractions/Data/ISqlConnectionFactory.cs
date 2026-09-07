namespace Bookify.Application.Abstractions.Data
{
    using System.Data;

    public interface ISqlConnectionFactory
    {
        IDbConnection CreateConnection();
    }
}