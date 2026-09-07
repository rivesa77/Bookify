namespace Bookify.Application.Apartments.SearchApartments
{
    public sealed class AddressResponse
    {
        public string Country { get; init; } = String.Empty;

        public string State { get; init; } = String.Empty;

        public string ZipCode { get; init; } = String.Empty;

        public string City { get; init; } = String.Empty;

        public string Street { get; init; } = String.Empty;
    }
}