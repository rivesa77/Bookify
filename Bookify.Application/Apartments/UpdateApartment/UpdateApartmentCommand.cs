namespace Bookify.Application.Apartments.UpdateApartment
{
    using Bookify.Application.Abstractions.Messaging;
    using Bookify.Domain.Apartments;

    public sealed record UpdateApartmentCommand(
        Guid ApartmentId,
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
        List<Amenity> Amenities) : ICommand;
}
