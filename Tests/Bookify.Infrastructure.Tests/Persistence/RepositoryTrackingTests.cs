namespace Bookify.Infrastructure.Tests.Persistence
{
    using Bookify.Domain.Apartments;
    using Bookify.Domain.Bookings;
    using Bookify.Domain.Reviews;
    using Bookify.Domain.Users;
    using Bookify.Infrastructure.Repositories;
    using Bookify.Infrastructure.Tests.Support;
    using FluentAssertions;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Infrastructure")]
    public sealed class RepositoryTrackingTests
    {
        private readonly ApplicationDbContext context = InfrastructureTestData.CreateContext();

        [TestCleanup]
        public void Cleanup() => context.Dispose();

        [TestMethod]
        public void UserRepositoryAdd_Should_AttachExistingRoleWithoutInsertingIt()
        {
            // Arrange
            User user = InfrastructureTestData.CreateUser();

            UserRepository repository = new(context);

            // Act
            repository.Add(user);

            // Assert
            context.Entry(user).State.Should().Be(EntityState.Added);

            context.Entry(user.Roles.Single()).State.Should().Be(EntityState.Unchanged);

            context.ChangeTracker.Entries<Role>().Should().ContainSingle();

            user.GetDomainEvents().Should().ContainSingle();
        }

        [TestMethod]
        public void RepositoriesAdd_Should_TrackAggregatesWithoutSavingOrClearingEvents()
        {
            // Arrange
            Apartment apartment = InfrastructureTestData.CreateApartment();

            Booking booking = InfrastructureTestData.CreateBooking(apartment, Guid.NewGuid());

            booking.Confirm(InfrastructureTestData.UtcNow.AddMinutes(1));

            booking.Complete(InfrastructureTestData.UtcNow.AddMonths(2));

            Review review = Review.Create(
                booking,
                Rating.Create(5).Value,
                new Comment("Good stay"),
                InfrastructureTestData.UtcNow.AddMonths(2)).Value;

            // Act
            new ApartmentRepository(context).Add(apartment);

            new BookingRepository(context).Add(booking);

            new ReviewRepository(context).Add(review);

            // Assert
            context.Entry(apartment).State.Should().Be(EntityState.Added);

            context.Entry(booking).State.Should().Be(EntityState.Added);

            context.Entry(review).State.Should().Be(EntityState.Added);

            booking.GetDomainEvents().Should().HaveCount(3);

            review.GetDomainEvents().Should().ContainSingle();

            context.Database.GetDbConnection().State.Should().Be(System.Data.ConnectionState.Closed);
        }
    }
}