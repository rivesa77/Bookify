namespace Bookify.Domain.Abstractions
{
    using System.Diagnostics.CodeAnalysis;

    public class Result
    {
        public bool IsSuccess { get; }

        public Error Error { get; }

        public bool IsFailure => !IsSuccess;

        protected internal Result(bool isSuccess, Error error)
        {
            if (isSuccess && error != Error.None)
            {
                throw new InvalidOperationException();
            }

            if (!isSuccess && error == Error.None)
            {
                throw new InvalidOperationException();
            }

            IsSuccess = isSuccess;
            Error = error;
        }

        public static Result Success()
        {
            return new Result(true, Error.None);
        }

        public static Result Failure(Error error)
        {
            return new Result(false, error);
        }

        public static Result<TValue> Success<TValue>(TValue value)
        {
            return new(value, true, Error.None);
        }

        public static Result<TValue> Failure<TValue>(Error error)
        {
            return new(default, false, error);
        }

        public static Result<TValue> Create<TValue>(TValue? value) => value is not null ? Success(value) : Failure<TValue>(Error.NullValue);
    }

    public class Result<TValue> : Result
    {
        protected internal Result(
            TValue? tValue,
            bool isSuccess,
            Error error)
            : base(isSuccess, error)
        {
            Value = tValue;
        }

        [NotNull]
        public TValue Value => IsSuccess ? field! : throw new InvalidOperationException("The value of a failure result can not be accessed.");

        public static implicit operator Result<TValue>(TValue? value) => Create(value);
    }
}