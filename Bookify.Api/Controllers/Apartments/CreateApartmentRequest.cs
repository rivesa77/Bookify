namespace Bookify.Api.Controllers.Apartment
{
    using Bookify.Domain.Apartments;

    public sealed record CreateApartmentRequest(
        string Name,
        string Description,
        string Country,
        string State,
        string ZipCode,
        string City,
        string Street,
        decimal PriceAmount,
        decimal CleaningFeeAmount,
        string Currency,
        List<Amenity> Amenities);
}
