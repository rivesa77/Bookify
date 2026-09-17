namespace Bookify.Domain.Tests.Apartments
{
    using Bookify.Domain.Apartments;
    using Bookify.Domain.Commons;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Domain")]
    public sealed class ApartmentTests
    {
        private static readonly Name ApartmentName = Name.Create("Test apartment").Value;

        private static readonly Description Description = new("City apartment");

        private static readonly Address Address = new(
            "Spain",
            "Madrid",
            "28001",
            "Madrid",
            "Street 1");

        private static readonly Money Price = new(100m, Currency.Eur);

        private static readonly Money CleaningFee = new(20m, Currency.Eur);

        [TestMethod]
        public void Constructor_Should_PreserveAllPropertiesAndStartUnbooked()
        {
            // Arrange
            Guid id = Guid.NewGuid();

            List<Amenity> amenities = [Amenity.Wifi, Amenity.Parking];

            // Act
            Apartment apartment = new(
                id,
                ApartmentName,
                Description,
                Address,
                Price,
                CleaningFee,
                amenities);

            // Assert
            apartment.Id.Should().Be(id);

            apartment.Name.Should().Be(ApartmentName);

            apartment.Description.Should().Be(Description);

            apartment.Address.Should().Be(Address);

            apartment.Price.Should().Be(Price);

            apartment.CleaningFeeAmount.Should().Be(CleaningFee);

            apartment.Amenities.Should().Equal(amenities);

            apartment.LastBookedOnUTC.Should().BeNull();

            apartment.GetDomainEvents().Should().BeEmpty();
        }

        [TestMethod]
        public void Address_Should_PreserveComponentsAndUseValueEquality()
        {
            // Arrange
            Address expected = Address;

            // Act
            Address copy = expected with { };

            Address changed = expected with { Street = "Street 2" };

            // Assert
            copy.Country.Should().Be("Spain");

            copy.State.Should().Be("Madrid");

            copy.ZipCode.Should().Be("28001");

            copy.City.Should().Be("Madrid");

            copy.Street.Should().Be("Street 1");

            copy.Should().Be(expected);

            changed.Should().NotBe(expected);
        }
    }
}
