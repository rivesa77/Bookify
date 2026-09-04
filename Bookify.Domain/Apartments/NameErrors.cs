namespace Bookify.Domain.Apartments
{
    using Bookify.Domain.Abstractions;

    public static class NameErrors
    {
        public static readonly Error Empty = new(
            "Name.Empty",
            "The name is mandatory.");

        public static readonly Error InvalidLength = new(
            "Name.InvalidLength",
            "The length max 75.");
    }
}