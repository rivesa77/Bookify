namespace Bookify.Application.Tests.Validation
{
    using Bookify.Application.Apartments.CreateApartment;
    using Bookify.Application.Bookings.ReserveBooking;
    using Bookify.Application.Reviews.CreateReview;
    using Bookify.Application.Users.CreateUser;
    using Bookify.Application.Users.LogInUser;
    using Bookify.Domain.Apartments;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Application")]
    public sealed class CommandValidatorTests
    {
        private const string FirstName = "Ana";

        private const string LastName = "Garcia";

        private const string Email = "ana@example.com";

        private const string Password = "12345";

        private static readonly DateOnly StartDate = new(
            2026,
            10,
            1);

        private static CreateUserCommand ValidUserCommand() => new(
            FirstName,
            LastName,
            Email,
            Password);

        private static LogInUserCommand ValidLoginCommand() => new(Email, Password);

        private static CreateReviewCommand ValidReviewCommand() => new(
            Guid.NewGuid(),
            3,
            "Good stay");

        private readonly CreateUserCommandHandlerValidator createUserCommandHandlerValidator = new();

        private readonly LogInUserCommandHandlerValidator logInUserCommandHandlerValidator = new();

        private readonly CreateApartmentCommandValidator createApartmentCommandValidator = new();

        private readonly CreateReviewCommandValidator createReviewCommandValidator = new();

        private readonly ReserveBookingCommandValidator reserveBookingCommandValidator = new();

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow(" ")]
        [DataRow("invalid")]
        [DataRow("@example.com")]
        [DataRow("ana@")]
        public void UserValidators_Should_RejectInvalidEmail(string? email)
        {
            // Arrange
            CreateUserCommand create = ValidUserCommand() with { Email = email! };

            LogInUserCommand login = ValidLoginCommand() with { Email = email! };

            // Act

            FluentValidation.Results.ValidationResult createResult = createUserCommandHandlerValidator.Validate(create);

            FluentValidation.Results.ValidationResult loginResult = logInUserCommandHandlerValidator.Validate(login);

            // Assert

            createResult.Errors.Should().Contain(e => e.PropertyName == "Email");

            loginResult.Errors.Should().Contain(e => e.PropertyName == "Email");
        }

        [TestMethod]
        [DataRow(null, false)]
        [DataRow("", false)]
        [DataRow("     ", false)]
        [DataRow("1234", false)]
        [DataRow("12345", true)]
        [DataRow("1234567890", true)]
        [DataRow("12345678901", false)]
        public void UserValidators_Should_EnforcePasswordBoundaries(string? password, bool valid)
        {
            // Arrange
            CreateUserCommand create = ValidUserCommand() with { Password = password! };

            LogInUserCommand login = ValidLoginCommand() with { Password = password! };

            // Act

            FluentValidation.Results.ValidationResult createResult = createUserCommandHandlerValidator.Validate(create);

            FluentValidation.Results.ValidationResult loginResult = logInUserCommandHandlerValidator.Validate(login);

            // Assert

            createResult.IsValid.Should().Be(valid);

            loginResult.IsValid.Should().Be(valid);
        }

        [TestMethod]
        [DataRow("FirtsName", 0, false)]
        [DataRow("FirtsName", 100, true)]
        [DataRow("FirtsName", 101, false)]
        [DataRow("LastName", 0, false)]
        [DataRow("LastName", 100, true)]
        [DataRow("LastName", 101, false)]
        public void CreateUser_Should_EnforceNameBoundaries(
            string field,
            int length,
            bool valid)
        {
            // Arrange
            CreateUserCommand command = ValidUserCommand();

            command = field == "FirtsName" ? command with { FirtsName = new string('a', length) }
            : command with { LastName = new string('a', length) };

            // Act

            FluentValidation.Results.ValidationResult result = createUserCommandHandlerValidator.Validate(command);

            // Assert

            result.IsValid.Should().Be(valid);

            if (!valid)
            {
                result.Errors.Should().Contain(e => e.PropertyName == field);
            }
        }

        [TestMethod]
        [DataRow(-1, false)]
        [DataRow(0, false)]
        [DataRow(1, true)]
        public void ReserveBooking_Should_RequireEndAfterStart(int days, bool valid)
        {
            // Arrange
            ReserveBookingCommand command = new(
                Guid.NewGuid(),
                Guid.NewGuid(),
                StartDate,
                StartDate.AddDays(days));

            // Act

            FluentValidation.Results.ValidationResult result = reserveBookingCommandValidator.Validate(command);

            // Assert

            result.IsValid.Should().Be(valid);
        }

        [TestMethod]
        public void ReserveBooking_Should_RejectBothEmptyIdentifiers()
        {
            // Arrange
            ReserveBookingCommand command = new(
                Guid.Empty,
                Guid.Empty,
                StartDate,
                StartDate.AddDays(1));

            // Act

            FluentValidation.Results.ValidationResult result = reserveBookingCommandValidator.Validate(command);

            // Assert

            result.Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(["UserId", "ApartmentId"]);
        }

        [TestMethod]
        [DataRow(0, false)]
        [DataRow(1, true)]
        [DataRow(5, true)]
        [DataRow(6, false)]
        public void CreateReview_Should_EnforceRatingRange(int rating, bool valid)
        {
            // Arrange
            CreateReviewCommand command = ValidReviewCommand() with { Rating = rating };

            // Act

            FluentValidation.Results.ValidationResult result = createReviewCommandValidator.Validate(command);

            // Assert

            result.IsValid.Should().Be(valid);
        }

        [TestMethod]
        [DataRow(0, false)]
        [DataRow(1, true)]
        [DataRow(200, true)]
        [DataRow(201, false)]
        public void CreateReview_Should_EnforceCommentLength(int length, bool valid)
        {
            // Arrange
            CreateReviewCommand command = ValidReviewCommand() with { Comment = new string('a', length) };

            // Act

            FluentValidation.Results.ValidationResult result = createReviewCommandValidator.Validate(command);

            // Assert

            result.IsValid.Should().Be(valid);
        }

        public static IEnumerable<object[]> InvalidApartmentText()
        {
            foreach (string field in new[] {
                "Name",
                "Description",
                "Country",
                "State",
                "ZipCode",
                "City",
                "Street" })
            {
                foreach (string? value in new string?[] {
                    null,
                    "",
                    " " })
                {
                    yield return [field, value!];
                }
            }
        }

        [TestMethod]
        [DynamicData(nameof(InvalidApartmentText))]
        public void CreateApartment_Should_RequireTextFields(string field, string? value)
        {
            // Arrange
            CreateApartmentCommand command = ApartmentCommand();

            command = field switch
            {
                "Name" => command with { Name = value! },
                "Description" => command with { Description = value! },
                "Country" => command with { Country = value! },
                "State" => command with { State = value! },
                "ZipCode" => command with { ZipCode = value! },
                "City" => command with { City = value! },
                _ => command with { Street = value! }
            };

            // Act

            FluentValidation.Results.ValidationResult result = createApartmentCommandValidator.Validate(command);

            // Assert

            result.Errors.Should().Contain(e => e.PropertyName == field);
        }

        [TestMethod]
        [DataRow("EUR", true)]
        [DataRow("USD", true)]
        [DataRow("eur", false)]
        [DataRow("GBP", false)]
        [DataRow("", false)]
        [DataRow(null, false)]
        public void CreateApartment_Should_ValidateCurrency(string? currency, bool valid)
        {
            // Arrange
            CreateApartmentCommand command = ApartmentCommand() with { Currency = currency! };

            // Act

            FluentValidation.Results.ValidationResult result = createApartmentCommandValidator.Validate(command);

            // Assert

            result.IsValid.Should().Be(valid);
        }

        [TestMethod]
        [DataRow(200, 2000, true)]
        [DataRow(201, 2000, false)]
        [DataRow(200, 2001, false)]
        public void CreateApartment_Should_EnforceTextLength(
            int nameLength,
            int descriptionLength,
            bool valid)
        {
            // Arrange
            CreateApartmentCommand command = ApartmentCommand() with
            {
                Name = new string('a', nameLength),

                Description = new string('a', descriptionLength)
            };

            // Act

            FluentValidation.Results.ValidationResult result = createApartmentCommandValidator.Validate(command);

            // Assert

            result.IsValid.Should().Be(valid);
        }

        [TestMethod]
        [DataRow(0, 0, false)]
        [DataRow(-1, 0, false)]
        [DataRow(1, -1, false)]
        [DataRow(1, 0, true)]
        public void CreateApartment_Should_ValidateAmounts(
            int price,
            int cleaning,
            bool valid)
        {
            // Arrange
            CreateApartmentCommand command = ApartmentCommand() with
            {
                PriceAmount = price,

                CleaningFeeAmount = cleaning
            };

            // Act

            FluentValidation.Results.ValidationResult result = createApartmentCommandValidator.Validate(command);

            // Assert

            result.IsValid.Should().Be(valid);
        }

        internal static CreateApartmentCommand ApartmentCommand() => new(
            "Apartment",
            "City apartment",
            "Spain",
            "Madrid",
            "28001",
            "Madrid",
            "Street 1",
            100,
            20,
            "EUR",
            [Amenity.Wifi]);
    }
}