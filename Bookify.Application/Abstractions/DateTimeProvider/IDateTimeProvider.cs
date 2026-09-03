namespace Bookify.Application.Abstractions.DateTimeProvider
{
    using System;

    public interface IDateTimeProvider
    {
        DateTime UtcNow { get; }
    }
}