namespace Bookify.Infrastructure.Tests.Persistence
{
    using System.Data;
    using Bookify.Application.Exceptions;
    using Bookify.Domain.Apartments;
    using Bookify.Domain.Bookings;
    using Bookify.Domain.Reviews;
    using Bookify.Domain.Users;
    using Bookify.Infrastructure.Data;
    using Bookify.Infrastructure.Repositories;
    using Bookify.Infrastructure.Tests.Support;
    using Dapper;
    using FluentAssertions;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Migrations;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("PostgreSQL")]
    public sealed class PostgresRepositoryTests : PostgresTestBase
    {
        [TestMethod]
        public async Task Repositories_Should_RoundTripAllAggregatesAndReturnNullForMissingIds()
        {
            // Arrange
            (User user, Apartment apartment, Booking booking) = await SeedBooking(BookingStatus.Completed);

            Review review = Review.Create(
                booking,
                Rating.Create(5).Value,
                new Comment("Good stay"),
                InfrastructureTestData.UtcNow.AddMonths(2)).Value;

            ReviewRepository reviews = new(Context);

            reviews.Add(review);

            await Context.SaveChangesAsync();

            Context.ChangeTracker.Clear();

            // Act
            Apartment? storedApartment = await new ApartmentRepository(Context).GetByIdAsync(apartment.Id);

            Booking? storedBooking = await new BookingRepository(Context).GetByIdAsync(booking.Id);

            User? storedUser = await new UserRepository(Context).GetByIdAsync(user.Id);

            Review? storedReview = await reviews.GetByIdAsync(review.Id);

            // Assert
            storedApartment.Should().BeEquivalentTo(apartment);

            storedBooking.Should().BeEquivalentTo(booking);

            storedUser!.Email.Should().Be(user.Email);

            storedUser.IdentityId.Should().Be(user.IdentityId);

            storedReview.Should().BeEquivalentTo(review);

            (await reviews.GetByIdAsync(Guid.NewGuid())).Should().BeNull();

            (await new ApartmentRepository(Context).GetByIdAsync(Guid.NewGuid())).Should().BeNull();

            (await new BookingRepository(Context).GetByIdAsync(Guid.NewGuid())).Should().BeNull();

            (await new UserRepository(Context).GetByIdAsync(Guid.NewGuid())).Should().BeNull();

            (await Context.Set<Role>().CountAsync()).Should().Be(1);
        }

        [TestMethod]
        [DataRow(BookingStatus.Reserved, 0, 4, true)]
        [DataRow(BookingStatus.Confirmed, 0, 4, true)]
        [DataRow(BookingStatus.Completed, 0, 4, true)]
        [DataRow(BookingStatus.Rejected, 0, 4, false)]
        [DataRow(BookingStatus.Cancelled, 0, 4, false)]
        [DataRow(BookingStatus.Reserved, -3, -1, false)]
        [DataRow(BookingStatus.Reserved, 5, 7, false)]
        [DataRow(BookingStatus.Reserved, -1, 0, true)]
        [DataRow(BookingStatus.Reserved, 4, 5, true)]
        [DataRow(BookingStatus.Reserved, 1, 2, true)]
        [DataRow(BookingStatus.Reserved, -1, 5, true)]
        public async Task IsOverlapping_Should_RespectStatusesAndInclusiveBoundaries(
            BookingStatus status,
            int startOffset,
            int endOffset,
            bool expected)
        {
            // Arrange
            (User user, Apartment apartment, Booking booking) = await SeedBooking(status);

            DateRange range = DateRange.Create(
                InfrastructureTestData.StartDate.AddDays(startOffset),
                InfrastructureTestData.StartDate.AddDays(endOffset));

            BookingRepository repository = new(Context);

            // Act
            bool overlaps = await repository.IsOverlappingAsync(apartment, range);

            bool otherApartment = await repository.IsOverlappingAsync(InfrastructureTestData.CreateApartment(), range);

            // Assert
            overlaps.Should().Be(expected);

            otherApartment.Should().BeFalse();
        }

        [TestMethod]
        public async Task ApartmentVersion_Should_DetectCompetingUpdates()
        {
            // Arrange
            (User user, Apartment apartment, Booking booking) = await SeedBooking();

            await using ApplicationDbContext first = InfrastructureTestData.CreateContext(connectionString: ConnectionString);

            await using ApplicationDbContext second = InfrastructureTestData.CreateContext(connectionString: ConnectionString);

            Apartment firstCopy = (await new ApartmentRepository(first).GetByIdAsync(apartment.Id))!;

            Apartment secondCopy = (await new ApartmentRepository(second).GetByIdAsync(apartment.Id))!;

            firstCopy.Amenities.Add(Amenity.Gym);

            secondCopy.Amenities.Add(Amenity.Spa);

            await first.SaveChangesAsync();

            // Act
            Func<Task> act = () => second.SaveChangesAsync();

            // Assert
            (await act.Should().ThrowAsync<ConcurrencyException>()).Which.InnerException.Should().BeOfType<DbUpdateConcurrencyException>();
        }

        [TestMethod]
        public async Task SqlConnectionFactory_Should_OpenDatabaseAndRoundTripDapperDateOnly()
        {
            // Arrange
            SqlConnectionFactory factory = new(ConnectionString);

            SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());

            // Act
            using IDbConnection connection = factory.CreateConnection();

            string database = await connection.QuerySingleAsync<string>("select current_database()");

            // Assert
            connection.State.Should().Be(ConnectionState.Open);

            database.Should().StartWith("bookify_test_");

            (await connection.QuerySingleAsync<DateOnly>("select @date::date", new { date = InfrastructureTestData.StartDate }))
                .Should().Be(InfrastructureTestData.StartDate);
        }

        [TestMethod]
        public async Task Migrations_Should_ApplyRollbackAndReapplyWithSeedData()
        {
            // Arrange
            IMigrator migrator = Context.GetService<IMigrator>();

            // Act
            await migrator.MigrateAsync("0");

            await migrator.MigrateAsync();

            // Assert
            (await Context.Database.GetPendingMigrationsAsync()).Should().BeEmpty();

            (await Context.Set<Role>().SingleAsync()).Name.Should().Be(Role.Registered.Name);

            (await Context.Set<Permission>().SingleAsync()).Name.Should().Be(Permission.UserRead.Name);

            RolePermission link = await Context.Set<RolePermission>().SingleAsync();

            link.RoleId.Should().Be(Role.Registered.Id);

            link.PermissionId.Should().Be(Permission.UserRead.Id);
        }
    }
}
