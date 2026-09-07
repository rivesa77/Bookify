namespace Bookify.Infrastructure.Clock
{
    using System;
    using Bookify.Application.Abstractions.DateTimeProvider;

    internal sealed class DateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}