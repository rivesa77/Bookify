namespace Bookify.Application.Apartments.CreateApartment
{
    using Bookify.Application.Abstractions.Messaging;
    using Bookify.Domain.Apartments;

    public sealed record CreateApartmentCommand(
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
        List<Amenity> Amenities) : ICommand<Guid>;
}
