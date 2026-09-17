namespace Bookify.Application.Tests.Apartments
{
    using Bookify.Application.Apartments.SearchApartments;
    using Bookify.Application.Tests.Support;
    using Bookify.Domain.Bookings;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Application")]
    public sealed class SearchApartmentsTests
    {
        private static readonly DateOnly StartDate = new(
            2026,
            10,
            1);

        private static readonly DateOnly EndDate = StartDate.AddDays(4);

        private readonly Bookify.Application.Tests.Support.ApplicationTestContext context = new();

        [TestCleanup]
        public void Cleanup()
        {
            context.Dispose();
        }

        [TestMethod]
        public async Task Send_ReversedDates_Should_ReturnEmptyWithoutConnection()
        {
            // Arrange

            SearchApartmentsQuery query = new(EndDate, StartDate);

            // Act

            Domain.Abstractions.Result<IReadOnlyList<ApartmentResponse>> result = await context.Sender.Send(query);

            // Assert

            result.IsSuccess.Should().BeTrue();

            result.Value.Should().BeEmpty();

            context.Sql.VerifyNoOtherCalls();
        }

        [TestMethod]
        [DataRow(0, false)]
        [DataRow(2, false)]
        [DataRow(1, true)]
        public async Task Send_Should_MapApartmentsWithAddressesAndBindFilters(int count, bool sameDay)
        {
            // Arrange

            using SqlQueryStub sql = CreateSqlQuery();

            List<ApartmentResponse> expected = new();

            for (int i = 0; i < count; i++)
            {
                ApartmentResponse apartment = new()
                {
                    Id = Guid.NewGuid(),

                    Name = $"Apartment {i}",

                    Description = $"Description {i}",

                    Price = 100 + i,

                    Currency = "EUR",

                    Address = new AddressResponse
                    {
                        Country = "Spain",

                        State = "Madrid",

                        ZipCode = "28001",

                        City = "Madrid",

                        Street = $"Street {i}"
                    }
                };

                expected.Add(apartment);

                sql.AddRow(
                    apartment.Id,
                    apartment.Name,
                    apartment.Description,
                    apartment.Price,
                    apartment.Currency,
                    apartment.Address.Country,
                    apartment.Address.State,
                    apartment.Address.ZipCode,
                    apartment.Address.City,
                    apartment.Address.Street);
            }

            DateOnly end = sameDay ? StartDate : EndDate;

            // Act

            Domain.Abstractions.Result<IReadOnlyList<ApartmentResponse>> result = await context.Sender.Send(new SearchApartmentsQuery(StartDate, end));

            // Assert

            result.Value.Should().BeEquivalentTo(expected, options => options.WithStrictOrdering());

            sql.Parameter("StartDate").Should().Be(StartDate);

            sql.Parameter("EndDate").Should().Be(end);

            sql.ParameterValues.OfType<int>().Should().BeEquivalentTo(
            [(int)BookingStatus.Reserved, (int)BookingStatus.Confirmed, (int)BookingStatus.Completed]);

            sql.Disposed.Should().BeTrue();
        }

        [TestMethod]
        public async Task Send_DatabaseFailure_Should_PropagateAndDisposeConnection()
        {
            // Arrange

            using SqlQueryStub sql = CreateSqlQuery();

            sql.FailWith(new InvalidOperationException("Offline"));

            // Act

            Func<Task> act = () => context.Sender.Send(new SearchApartmentsQuery(StartDate, EndDate));

            // Assert

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Offline");

            sql.Disposed.Should().BeTrue();
        }

        private SqlQueryStub CreateSqlQuery()
        {
            SqlQueryStub sql = ApartmentTable();

            context.Sql.Setup(f => f.CreateConnection()).Returns(sql.Connection.Object);

            return sql;
        }

        private static SqlQueryStub ApartmentTable() => new(
            ("Id", typeof(Guid)),
            ("Name", typeof(string)),
            ("Description", typeof(string)),
            ("Price", typeof(decimal)),
            ("Currency", typeof(string)),
            ("Country", typeof(string)),
            ("State", typeof(string)),
            ("ZipCode", typeof(string)),
            ("City", typeof(string)),
            ("Street", typeof(string)));
    }
}