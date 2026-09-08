namespace Bookify.Application.Exceptions
{
    using System;

    public sealed class CurrencyException : Exception
    {
        public CurrencyException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}