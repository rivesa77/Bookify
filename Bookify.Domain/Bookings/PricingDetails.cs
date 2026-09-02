namespace Bookify.Domain.Bookings
{
    using Bookify.Domain.Commons;

    public record PricingDetails(
        Money PriceForPeriod,
        Money CleaningFee,
        Money AmenitiesUpCharge,
        Money TotalPrice)
    {
    }
}