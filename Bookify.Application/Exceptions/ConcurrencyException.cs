namespace Bookify.Application.Exceptions
{
    using System;

    public sealed class ConcurrencyException : Exception
    {
        public ConcurrencyException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}