namespace Bookify.Api.Controllers.Apartments
{
    using Bookify.Domain.Apartments;

    public sealed record UpdateApartmentRequest(
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
