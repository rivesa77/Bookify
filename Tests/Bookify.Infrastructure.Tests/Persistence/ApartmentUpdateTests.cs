namespace Bookify.Infrastructure.Tests.Persistence
{
    using Bookify.Domain.Apartments;
    using Bookify.Domain.Commons;
    using Bookify.Infrastructure.Tests.Support;
    using FluentAssertions;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Infrastructure")]
    public sealed class ApartmentUpdateTests
    {
        [TestMethod]
        public void Update_TrackedApartment_Should_DetectChangesWithoutAddingAnotherApartment()
        {
            // Arrange

            using ApplicationDbContext context = InfrastructureTestData.CreateContext();

            Apartment apartment = InfrastructureTestData.CreateApartment();

            context.Attach(apartment);

            // Act

            apartment.Update(
                Name.Create("Updated apartment").Value,
                new Description("Updated description"),
                apartment.Address with { Street = "Street 2" },
                new Money(150m, Currency.Usd),
                new Money(30m, Currency.Usd),
                [Amenity.Wifi]);

            context.ChangeTracker.DetectChanges();

            // Assert

            context.ChangeTracker.Entries<Apartment>().Should().ContainSingle();

            context.Entry(apartment).State.Should().Be(EntityState.Modified);

            context.Entry(apartment).Property(entity => entity.Name).IsModified.Should().BeTrue();

            context.Entry(apartment).Property(entity => entity.LastBookedOnUTC).IsModified.Should().BeFalse();

            context.Entry(apartment).Property("Version").Metadata.IsConcurrencyToken.Should().BeTrue();

            context.Database.GetDbConnection().State.Should().Be(System.Data.ConnectionState.Closed);
        }
    }
}
