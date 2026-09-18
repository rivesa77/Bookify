namespace Bookify.Infrastructure.Tests.Support
{
    using Bookify.Domain.Apartments;
    using Bookify.Domain.Bookings;
    using Bookify.Domain.Commons;
    using Bookify.Domain.Users;
    using Bookify.Infrastructure.Authentication;
    using MediatR;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Moq;

    internal static class InfrastructureTestData
    {
        internal const string ConnectionString = "Host=127.0.0.1;Port=1;Database=bookify_tests;Username=test;Password=test;Timeout=1";

        internal const string IdentityId = "external-user-id";

        internal const string Email = "ana@example.com";

        internal const string Password = "TestOnly1! &+";

        internal static readonly DateTime UtcNow = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

        internal static readonly DateOnly StartDate = new(2026, 10, 1);

        internal static KeycloakOptions Keycloak() => new()
        {
            AdminUrl = "https://identity.example/admin/realms/bookify/",
            TokenUrl = "https://identity.example/realms/bookify/protocol/openid-connect/token",
            AdminClientId = "admin-client",
            AdminClientSecret = "admin-test-secret",
            AuthClientId = "auth-client",
            AuthClientSecret = "auth-test-secret"
        };

        internal static ApplicationDbContext CreateContext(
            IPublisher? publisher = null,
            IInterceptor? interceptor = null,
            string connectionString = ConnectionString)
        {
            DbContextOptionsBuilder<ApplicationDbContext> options = new();

            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();

            if (interceptor is not null)
            {
                options.AddInterceptors(interceptor);
            }

            return new ApplicationDbContext(options.Options, publisher ?? Mock.Of<IPublisher>());
        }

        internal static User CreateUser()
        {
            User user = User.Create(
                new FirstName("Ana"),
                new LastName("Garcia"),
                new Email(Email));

            user.SetIdentityId(IdentityId);

            // Isolate mutable role navigations from the domain's shared static instance.
            ICollection<Role> roles = (ICollection<Role>)user.Roles;

            roles.Clear();

            roles.Add(new Role(Role.Registered.Id, Role.Registered.Name));

            return user;
        }

        internal static Apartment CreateApartment() => new(
            Guid.NewGuid(),
            Name.Create("Apartment").Value,
            new Description("City apartment"),
            new Address(
                "Spain",
                "Madrid",
                "28001",
                "Madrid",
                "Street 1"),
            new Money(100m, Currency.Eur),
            new Money(20m, Currency.Eur),
            [Amenity.Wifi, Amenity.Parking]);

        internal static Booking CreateBooking(Apartment apartment, Guid userId) => Booking.Reserve(
            apartment,
            userId,
            DateRange.Create(StartDate, StartDate.AddDays(4)),
            UtcNow,
            new PricingServices());
    }
}