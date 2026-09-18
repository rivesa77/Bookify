namespace Bookify.Infrastructure.Tests.Support
{
    using Bookify.Domain.Apartments;
    using Bookify.Domain.Bookings;
    using Bookify.Domain.Users;
    using Bookify.Infrastructure.Repositories;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Npgsql;

    public abstract class PostgresTestBase
    {
        private readonly string databaseName = "bookify_test_" + Guid.NewGuid().ToString("N");

        private string? adminConnectionString;

        private bool databaseCreated;

        protected ApplicationDbContext Context { get; private set; } = null!;

        protected string ConnectionString { get; private set; } = string.Empty;

        [TestInitialize]
        public async Task InitializeDatabase()
        {
            string? configured = Environment.GetEnvironmentVariable("BOOKIFY_TEST_POSTGRES");

            if (string.IsNullOrWhiteSpace(configured))
            {
                Assert.Inconclusive("Define BOOKIFY_TEST_POSTGRES for an isolated PostgreSQL test server with CREATEDB permission.");
            }

            NpgsqlConnectionStringBuilder admin = new(configured) { Pooling = false };

            adminConnectionString = admin.ConnectionString;

            await using NpgsqlConnection connection = new(adminConnectionString);

            await connection.OpenAsync();

            // The identifier is generated locally, never taken from the configured database name.
            await using NpgsqlCommand create = new($"CREATE DATABASE \"{databaseName}\"", connection);

            await create.ExecuteNonQueryAsync();

            databaseCreated = true;

            NpgsqlConnectionStringBuilder isolated = new(adminConnectionString)
            {
                Database = databaseName,

                Pooling = false
            };

            ConnectionString = isolated.ConnectionString;

            Context = InfrastructureTestData.CreateContext(connectionString: ConnectionString);

            await Context.Database.MigrateAsync();
        }

        [TestCleanup]
        public async Task CleanupDatabase()
        {
            if (Context is not null)
            {
                await Context.DisposeAsync();
            }

            if (!databaseCreated)
            {
                return;
            }

            await using NpgsqlConnection connection = new(adminConnectionString);

            await connection.OpenAsync();

            await using NpgsqlCommand drop = new($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", connection);

            await drop.ExecuteNonQueryAsync();
        }

        protected async Task<(User User, Apartment Apartment, Booking Booking)> SeedBooking(BookingStatus status = BookingStatus.Reserved)
        {
            User user = InfrastructureTestData.CreateUser();

            new UserRepository(Context).Add(user);

            Apartment apartment = InfrastructureTestData.CreateApartment();

            new ApartmentRepository(Context).Add(apartment);

            Booking booking = InfrastructureTestData.CreateBooking(apartment, user.Id);

            if (status == BookingStatus.Rejected)
            {
                booking.Reject(InfrastructureTestData.UtcNow.AddMinutes(1));
            }

            if (status is BookingStatus.Confirmed or BookingStatus.Cancelled or BookingStatus.Completed)
            {
                booking.Confirm(InfrastructureTestData.UtcNow.AddMinutes(1));
            }

            if (status == BookingStatus.Cancelled)
            {
                booking.Cancel(InfrastructureTestData.UtcNow.AddMinutes(2));
            }

            if (status == BookingStatus.Completed)
            {
                booking.Complete(InfrastructureTestData.StartDate.AddDays(4).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            }

            new BookingRepository(Context).Add(booking);

            await Context.SaveChangesAsync();

            Context.ChangeTracker.Clear();

            return (user, apartment, booking);
        }
    }
}
