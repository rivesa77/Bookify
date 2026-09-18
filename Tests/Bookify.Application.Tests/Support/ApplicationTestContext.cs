namespace Bookify.Application.Tests.Support
{
    using Bookify.Application.Abstractions.Authentication;
    using Bookify.Application.Abstractions.Data;
    using Bookify.Application.Abstractions.DateTimeProvider;
    using Bookify.Application.Abstractions.Email;
    using Bookify.Domain.Apartments;
    using Bookify.Domain.Bookings;
    using Bookify.Domain.Commons;
    using Bookify.Domain.Reviews;
    using Bookify.Domain.Users;
    using Bookify.TestUtilities.Context;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;

    internal sealed class ApplicationTestContext : ContextTestsBase
    {
        public static readonly DateTime UtcNow = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

        public Mock<IUserRepository> Users { get; } = new(MockBehavior.Strict);

        public Mock<IApartmentRepository> Apartments { get; } = new(MockBehavior.Strict);

        public Mock<IBookingRepository> Bookings { get; } = new(MockBehavior.Strict);

        public Mock<IReviewRepository> Reviews { get; } = new(MockBehavior.Strict);

        public Mock<IAuthenticationService> Authentication { get; } = new(MockBehavior.Strict);

        public Mock<IJwtService> Jwt { get; } = new(MockBehavior.Strict);

        public Mock<IUserContext> UserContext { get; } = new(MockBehavior.Strict);

        public Mock<ISqlConnectionFactory> Sql { get; } = new(MockBehavior.Strict);

        public Mock<IDateTimeProvider> Clock { get; } = new(MockBehavior.Strict);

        public Mock<IEmailService> Email { get; } = new(MockBehavior.Strict);

        public ApplicationTestContext()
        {
            Clock.SetupGet(c => c.UtcNow).Returns(UtcNow);

            RegisterContext(services =>
            {
                services.AddSingleton(Users.Object);

                services.AddSingleton(Apartments.Object);

                services.AddSingleton(Bookings.Object);

                services.AddSingleton(Reviews.Object);

                services.AddSingleton(Authentication.Object);

                services.AddSingleton(Jwt.Object);

                services.AddSingleton(UserContext.Object);

                services.AddSingleton(Sql.Object);

                services.AddSingleton(Clock.Object);

                services.AddSingleton(Email.Object);
            });
        }

        public static User CreateUser() => User.Create(
            new FirstName("Ana"),
            new LastName("Garcia"),
            new Email("ana@example.com"));

        public static Apartment CreateApartment() => new(
            Guid.NewGuid(),
            Name.Create("Apartment").Value,
            new Description("City apartment"),
            new Address(
                "Spain",
                "Madrid",
                "28001",
                "Madrid",
                "Street 1"),
            new Money(100, Currency.Eur),
            new Money(20, Currency.Eur),
            []);

        public static Booking CreateBooking(Guid userId) => Booking.Reserve(
            CreateApartment(),
            userId,
            DateRange.Create(
                new DateOnly(2026, 10, 1),
                new DateOnly(2026, 10, 5)),
            UtcNow,
            new PricingServices());
    }
}