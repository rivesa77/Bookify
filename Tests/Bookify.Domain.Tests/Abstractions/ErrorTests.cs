namespace Bookify.Domain.Tests.Abstractions
{
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Apartments;
    using Bookify.Domain.Bookings;
    using Bookify.Domain.Reviews;
    using Bookify.Domain.Users;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Domain")]
    public sealed class ErrorTests
    {
        private const string Code = "Test.Code";

        private const string Message = "Test message";

        [TestMethod]
        public void Error_Should_PreserveCodeAndMessageAndUseValueEquality()
        {
            // Arrange
            Error expected = new(Code, Message);

            // Act
            Error same = new(Code, Message);

            // Assert
            same.Code.Should().Be(Code);

            same.Name.Should().Be(Message);

            same.Should().Be(expected);

            same.Should().NotBe(new Error("Other.Code", Message));

            same.Should().NotBe(new Error(Code, "Other message"));
        }

        [TestMethod]
        public void None_Should_RepresentAbsenceOfError()
        {
            // Arrange
            Error expected = new(string.Empty, string.Empty);

            // Act
            Error none = Error.None;

            // Assert
            none.Should().Be(expected);
        }

        [TestMethod]
        [DynamicData(nameof(ErrorCatalog))]
        public void ErrorCatalog_Should_ExposeStableCodeAndNonEmptyMessage(Error error, string expectedCode)
        {
            // Arrange
            Error noError = Error.None;

            // Act
            string code = error.Code;

            // Assert
            code.Should().Be(expectedCode);

            error.Name.Should().NotBeNullOrWhiteSpace();

            error.Should().NotBe(noError);
        }

        public static IEnumerable<object[]> ErrorCatalog()
        {
            yield return [Error.NullValue, "Error.NullValue"];

            yield return [NameErrors.Empty, "Name.Empty"];

            yield return [NameErrors.InvalidLength, "Name.InvalidLength"];

            yield return [ApartmentErrors.NotFound, "Apartment.NotFound"];

            yield return [BookingErrors.NotFound, "Booking.NotFound"];

            yield return [BookingErrors.Overlap, "Booking.Overlap"];

            yield return [BookingErrors.NotReserved, "Booking.NotReserved"];

            yield return [BookingErrors.NotConfirmed, "Booking.NotConfirmed"];

            yield return [BookingErrors.AlreadyStarted, "Booking.AlreadyStarted"];

            yield return [ReviewErrors.NotEligible, "Review.NotEligible"];

            yield return [Rating.Invalid, "Rating.Invalid"];

            yield return [UserErrors.NotFound, "User.NotFound"];

            yield return [UserErrors.InvalidCredentials, "User.InvalidCredentials"];
        }
    }
}
