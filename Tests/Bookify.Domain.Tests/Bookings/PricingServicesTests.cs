namespace Bookify.Domain.Tests.Bookings
{
    using Bookify.Domain.Apartments;
    using Bookify.Domain.Bookings;
    using Bookify.Domain.Commons;
    using Bookify.Domain.Tests.Support;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Domain")]
    public sealed class PricingServicesTests
    {
        private readonly PricingServices service = new();

        private static readonly DateRange Period = DateRange.Create(DomainTestData.StartDate, DomainTestData.StartDate.AddDays(4));

        [TestMethod]
        [DataRow(Amenity.Wifi, 0)]
        [DataRow(Amenity.AirConditioning, 8)]
        [DataRow(Amenity.Parking, 4)]
        [DataRow(Amenity.PetFriendly, 0)]
        [DataRow(Amenity.SwimmingPool, 0)]
        [DataRow(Amenity.Gym, 0)]
        [DataRow(Amenity.Spa, 0)]
        [DataRow(Amenity.Terrace, 0)]
        [DataRow(Amenity.MountainView, 20)]
        [DataRow(Amenity.GardenView, 20)]
        public void CalculatePricing_Should_ApplyAmenitySurcharge(Amenity amenity, int surcharge)
        {
            // Arrange
            Apartment apartment = DomainTestData.CreateApartment(amenities: [amenity]);

            // Act
            PricingDetails result = service.CalculatePricing(apartment, Period);

            // Assert
            result.PriceForPeriod.Should().Be(new Money(400m, Currency.Eur));

            result.CleaningFee.Should().Be(new Money(20m, Currency.Eur));

            result.AmenitiesUpCharge.Should().Be(new Money(surcharge, Currency.Eur));

            result.TotalPrice.Should().Be(new Money(420m + surcharge, Currency.Eur));
        }

        [TestMethod]
        public void CalculatePricing_Should_SumSurchargesOnBasePrice()
        {
            // Arrange
            Apartment apartment = DomainTestData.CreateApartment(amenities:
                [Amenity.GardenView, Amenity.MountainView, Amenity.AirConditioning, Amenity.Parking, Amenity.Wifi]);

            // Act
            PricingDetails result = service.CalculatePricing(apartment, Period);

            // Assert
            result.AmenitiesUpCharge.Should().Be(new Money(52m, Currency.Eur));

            result.TotalPrice.Should().Be(new Money(472m, Currency.Eur));

            apartment.Price.Amount.Should().Be(100m);

            apartment.LastBookedOnUTC.Should().BeNull();

            apartment.GetDomainEvents().Should().BeEmpty();
        }

        [TestMethod]
        [DataRow(0, 0)]
        [DataRow(0, 20)]
        [DataRow(1, 0)]
        [DataRow(4, 20)]
        public void CalculatePricing_Should_ChargeCleaningOnceAndPreserveCurrency(int days, int cleaningFee)
        {
            // Arrange
            Apartment apartment = DomainTestData.CreateApartment(cleaningFee: cleaningFee, currency: Currency.Usd);

            DateRange period = DateRange.Create(DomainTestData.StartDate, DomainTestData.StartDate.AddDays(days));

            // Act
            PricingDetails result = service.CalculatePricing(apartment, period);

            // Assert
            result.PriceForPeriod.Should().Be(new Money(100m * days, Currency.Usd));

            result.AmenitiesUpCharge.Should().Be(Money.Zero(Currency.Usd));

            result.CleaningFee.Should().Be(new Money(cleaningFee, Currency.Usd));

            result.TotalPrice.Should().Be(new Money(100m * days + cleaningFee, Currency.Usd));
        }

        [TestMethod]
        public void CalculatePricing_Should_PreserveDecimalPrecision()
        {
            // Arrange
            Apartment apartment = DomainTestData.CreateApartment(
                price: 19.99m,
                cleaningFee: 1.25m,
                amenities: [Amenity.Parking]);

            // Act
            PricingDetails result = service.CalculatePricing(apartment, Period);

            // Assert
            result.PriceForPeriod.Amount.Should().Be(79.96m);

            result.AmenitiesUpCharge.Amount.Should().Be(0.7996m);

            result.TotalPrice.Amount.Should().Be(82.0096m);
        }
    }
}
