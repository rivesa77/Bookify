namespace Bookify.Domain.Tests.Bookings
{
    using Bookify.Domain.Bookings;
    using Bookify.Domain.Tests.Support;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Domain")]
    public sealed class DateRangeTests
    {
        private static readonly DateOnly StartDate = DomainTestData.StartDate;

        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(31)]
        [DataRow(365)]
        public void Create_Should_PreserveDatesAndCalculateDays(int days)
        {
            // Arrange
            DateOnly end = StartDate.AddDays(days);

            // Act
            DateRange range = DateRange.Create(StartDate, end);

            // Assert
            range.Start.Should().Be(StartDate);

            range.End.Should().Be(end);

            range.LengthInDays.Should().Be(days);

            range.Should().Be(DateRange.Create(StartDate, end));
        }

        [TestMethod]
        public void Create_EndBeforeStart_Should_Throw()
        {
            // Arrange
            DateOnly end = StartDate.AddDays(-1);

            // Act
            Func<DateRange> act = () => DateRange.Create(StartDate, end);

            // Assert
            act.Should().Throw<ApplicationException>().WithMessage("End date precedes start date.");
        }

        [TestMethod]
        public void LengthInDays_Should_IncludeLeapDay()
        {
            // Arrange
            DateOnly start = new(2024, 2, 28);
            DateOnly end = new(2024, 3, 1);

            // Act
            DateRange range = DateRange.Create(start, end);

            // Assert
            range.LengthInDays.Should().Be(2);
        }
    }
}