namespace Bookify.Domain.Apartments
{
    using Bookify.Domain.Abstractions;

    public sealed record Name
    {
        public const int ExactLength = 75;

        private Name(string value)
        {
            Value = value;
        }

        public string Value { get; }

        public static Result<Name> Create(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Result.Failure<Name>(NameErrors.Empty);
            }

            if (value.Length != ExactLength)
            {
                return Result.Failure<Name>(NameErrors.InvalidLength);
            }

            return new Name(value);
        }
    }
}