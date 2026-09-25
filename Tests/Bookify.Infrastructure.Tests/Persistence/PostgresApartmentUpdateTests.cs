namespace Bookify.Infrastructure.Tests.Persistence
{
    using Bookify.Domain.Apartments;
    using Bookify.Domain.Bookings;
    using Bookify.Domain.Commons;
    using Bookify.Domain.Users;
    using Bookify.Infrastructure.Repositories;
    using Bookify.Infrastructure.Tests.Support;
    using FluentAssertions;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("PostgreSQL")]
    public sealed class PostgresApartmentUpdateTests : PostgresTestBase
    {
        [TestMethod]
        public async Task Update_Should_PersistOwnedValuesAndPreserveExistingBookingPrices()
        {
            // Arrange

            (User user, Apartment original, Booking booking) = await SeedBooking();

            ApartmentRepository repository = new(Context);

            Apartment apartment = (await repository.GetByIdAsync(original.Id))!;

            Money bookingPrice = booking.PriceForPeriod;

            DateTime? lastBooked = apartment.LastBookedOnUTC;

            // Act

            apartment.Update(
                Name.Create("Updated apartment").Value,
                new Description("Updated description"),
                apartment.Address with { Street = "Street 2" },
                new Money(150m, Currency.Usd),
                new Money(30m, Currency.Usd),
                [Amenity.Wifi]);

            await Context.SaveChangesAsync();

            // Assert

            await using ApplicationDbContext verification = InfrastructureTestData.CreateContext(connectionString: ConnectionString);

            Apartment stored = await verification.Set<Apartment>().SingleAsync();

            stored.Should().BeEquivalentTo(apartment);

            stored.LastBookedOnUTC.Should().Be(lastBooked);

            Booking storedBooking = await verification.Set<Booking>().SingleAsync(entity => entity.Id == booking.Id);

            storedBooking.PriceForPeriod.Should().Be(bookingPrice);
        }
    }
}
