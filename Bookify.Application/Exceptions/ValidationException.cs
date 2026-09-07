namespace Bookify.Application.Exceptions
{
    using System;
    using System.Collections.Generic;

    public sealed class ValidationException : Exception
    {
        public ValidationException(IEnumerable<ValidationError> errors)
        {
            Errors = errors;
        }

        public IEnumerable<ValidationError> Errors { get; }
    }
}