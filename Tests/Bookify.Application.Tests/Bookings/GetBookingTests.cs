namespace Bookify.Application.Tests.Bookings
{
    using Bookify.Application.Bookings.GetBooking;
    using Bookify.Application.Tests.Support;
    using Bookify.Domain.Bookings;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("Application")]
    public sealed class GetBookingTests
    {
        private readonly Bookify.Application.Tests.Support.ApplicationTestContext context = new();

        [TestCleanup]
        public void Cleanup()
        {
            context.Dispose();
        }

        [TestMethod]
        [DataRow("owner")]
        [DataRow("other")]
        [DataRow("missing")]
        public async Task Send_Should_ReturnOnlyOwnersBooking(string scenario)
        {
            // Arrange

            using SqlQueryStub sql = CreateSqlQuery();

            Guid bookingId = Guid.NewGuid();

            Guid userId = Guid.NewGuid();

            Guid apartmentId = Guid.NewGuid();

            if (scenario != "missing")
            {
                sql.AddRow(
                    bookingId,
                    userId,
                    apartmentId,
                    1,
                    400m,
                    "EUR",
                    20m,
                    "EUR",
                    0m,
                    "EUR",
                    420m,
                    "EUR",
                    new DateTime(2026, 10, 1),
                    new DateTime(2026, 10, 5),
                    ApplicationTestContext.UtcNow);

                context.UserContext.SetupGet(u => u.UserId).Returns(scenario == "owner" ? userId : Guid.NewGuid());
            }

            // Act

            Domain.Abstractions.Result<BookingResponse> result = await context.Sender.Send(new GetBookingQuery(bookingId));

            // Assert

            if (scenario == "owner")
            {
                result.Value.Should().BeEquivalentTo(new BookingResponse
                {
                    Id = bookingId,
                    UserId = userId,
                    ApartmentId = apartmentId,
                    Status = 1,
                    PriceAmount = 400,
                    PriceCurrency = "EUR",
                    CleaningFeeAmount = 20,
                    CleaningFeeCurrency = "EUR",
                    AmenitiesUpChargeAmount = 0,
                    AmenitiesUpChargeCurrency = "EUR",
                    TotalPriceAmount = 420,
                    TotalPriceCurrency = "EUR",
                    DurationStart = new(
                        2026,
                        10,
                        1),
                    DurationEnd = new(
                        2026,
                        10,
                        5),
                    CreatedOnUtc = ApplicationTestContext.UtcNow
                });
            }
            else
            {
                result.IsFailure.Should().BeTrue();

                result.Error.Should().Be(BookingErrors.NotFound);
            }

            sql.Parameter("BookingId").Should().Be(bookingId);

            sql.Sql.Should().Contain("WHERE id = @BookingId");

            sql.Disposed.Should().BeTrue();

            context.Sql.Verify(f => f.CreateConnection(), Times.Once);

            if (scenario == "missing")
            {
                context.UserContext.VerifyNoOtherCalls();
            }
        }

        [TestMethod]
        public async Task Send_MissingUserContext_Should_PropagateAndDisposeConnection()
        {
            // Arrange

            using SqlQueryStub sql = CreateSqlQuery(identifiersOnly: true);

            sql.AddRow(Guid.NewGuid(), Guid.NewGuid());

            context.UserContext.SetupGet(u => u.UserId).Throws(new ApplicationException("User identifier is unavailable"));

            // Act

            Func<Task> act = () => context.Sender.Send(new GetBookingQuery(Guid.NewGuid()));

            // Assert

            await act.Should().ThrowAsync<ApplicationException>();

            sql.Disposed.Should().BeTrue();
        }

        [TestMethod]
        public async Task Send_DatabaseFailure_Should_PropagateAndDisposeConnection()
        {
            // Arrange

            using SqlQueryStub sql = CreateSqlQuery();

            InvalidOperationException error = new("Database unavailable");

            sql.FailWith(error);

            // Act

            Func<Task> act = () => context.Sender.Send(new GetBookingQuery(Guid.NewGuid()));

            // Assert

            (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(error);

            sql.Disposed.Should().BeTrue();

            context.UserContext.VerifyNoOtherCalls();
        }

        private SqlQueryStub CreateSqlQuery(bool identifiersOnly = false)
        {
            SqlQueryStub sql = identifiersOnly
            ? new SqlQueryStub(("Id", typeof(Guid)), ("UserId", typeof(Guid)))
            : BookingTable();

            context.Sql.Setup(f => f.CreateConnection()).Returns(sql.Connection.Object);

            return sql;
        }

        private static SqlQueryStub BookingTable() => new(
            ("Id", typeof(Guid)),
            ("UserId", typeof(Guid)),
            ("ApartmentId", typeof(Guid)),
            ("Status", typeof(int)),
            ("PriceAmount", typeof(decimal)),
            ("PriceCurrency", typeof(string)),
            ("CleaningFeeAmount", typeof(decimal)),
            ("CleaningFeeCurrency", typeof(string)),
            ("AmenitiesUpChargeAmount", typeof(decimal)),
            ("AmenitiesUpChargeCurrency", typeof(string)),
            ("TotalPriceAmount", typeof(decimal)),
            ("TotalPriceCurrency", typeof(string)),
            ("DurationStart", typeof(DateTime)),
            ("DurationEnd", typeof(DateTime)),
            ("CreatedOnUtc", typeof(DateTime)));
    }
}