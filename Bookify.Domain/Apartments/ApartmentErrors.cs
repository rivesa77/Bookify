namespace Bookify.Domain.Apartments
{
    using Bookify.Domain.Abstractions;

    public static class ApartmentErrors
    {
        public static readonly Error Conflict = new(
            "Apartment.Conflict",
            "The apartment was modified by another operation. Reload it before retrying.");

        public static readonly Error NotFound = new(
            "Apartment.NotFound",
            "The apartment with the specified identifier was not found");
    }
}
