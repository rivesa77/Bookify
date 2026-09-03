namespace Bookify.Domain.Bookings
{
    using Bookify.Domain.Apartments;
    using Bookify.Domain.Commons;

    public sealed class PricingServices
    {
        public PricingDetails CalculatePricing(Apartment apartments, DateRange period)
        {
            Currency currency = apartments.Price.Currency;

            Money priceForPeriod = new(apartments.Price.Amount * period.LengthInDays, currency);

            decimal percentageUpCharge = 0;

            foreach (Amenity amenity in apartments.Amenities)
            {
                percentageUpCharge += amenity switch
                {
                    Amenity.GardenView or Amenity.MontainView => 0.05m,
                    Amenity.AirConditioning => 0.02m,
                    Amenity.Parking => 0.01m,
                    _ => 0
                };
            }

            Money amenitiesUpCharge = Money.Zero(currency);

            if (percentageUpCharge > 0)
            {
                amenitiesUpCharge = new Money(priceForPeriod.Amount * percentageUpCharge, currency);
            }

            Money TotalPrice = Money.Zero(currency);

            TotalPrice += priceForPeriod;

            if (!apartments.CleaningFeeAmount.IsZero())
            {
                TotalPrice += apartments.CleaningFeeAmount;
            }

            TotalPrice += apartments.Price;

            return new PricingDetails(
                priceForPeriod,
                apartments.CleaningFeeAmount,
                amenitiesUpCharge,
                TotalPrice);
        }
    }
}