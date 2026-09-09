namespace Bookify.Api.MiddleWare
{
    public record ExceptionDetails(
        int Status,
        string Type,
        string Title,
        string Detail,
        IEnumerable<object>? Error)
    {
    }
}