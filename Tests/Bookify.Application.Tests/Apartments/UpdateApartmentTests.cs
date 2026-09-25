namespace Bookify.Application.Tests.Apartments
{
    using Bookify.Application.Apartments.UpdateApartment;
    using Bookify.Application.Exceptions;
    using Bookify.Application.Tests.Support;
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Apartments;
    using Bookify.Domain.Commons;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("Application")]
    public sealed class UpdateApartmentTests
    {
        private readonly ApplicationTestContext context = new();

        private readonly Apartment apartment = ApplicationTestContext.CreateApartment();

        [TestCleanup]
        public void Cleanup() => context.Dispose();

        [TestMethod]
        public async Task Send_Should_UpdateAllFieldsAndSaveOnceWithToken()
        {
            // Arrange

            UpdateApartmentCommand command = Command();

            using CancellationTokenSource cancellation = new();

            context.Apartments.Setup(repository => repository.GetByIdAsync(apartment.Id, cancellation.Token)).ReturnsAsync(apartment);

            context.UnitOfWork.Setup(unit => unit.SaveChangesAsync(cancellation.Token)).ReturnsAsync(1);

            // Act

            Result result = await context.Sender.Send(command, cancellation.Token);

            // Assert

            result.IsSuccess.Should().BeTrue();

            apartment.Id.Should().Be(command.ApartmentId);

            apartment.Name.Value.Should().Be(command.Name);

            apartment.Description.Value.Should().Be(command.Description);

            apartment.Address.Should().Be(new Address(
                command.Country,
                command.State,
                command.ZipCode,
                command.City,
                command.Street));

            apartment.Price.Should().Be(new Money(command.PriceAmount, Currency.Usd));

            apartment.CleaningFeeAmount.Should().Be(new Money(command.CleaningFeeAmount, Currency.Usd));

            apartment.Amenities.Should().Equal(command.Amenities);

            apartment.Amenities.Should().NotBeSameAs(command.Amenities);

            apartment.LastBookedOnUTC.Should().BeNull();

            context.Apartments.Verify(repository => repository.GetByIdAsync(apartment.Id, cancellation.Token), Times.Once);

            context.UnitOfWork.Verify(unit => unit.SaveChangesAsync(cancellation.Token), Times.Once);

            context.Apartments.VerifyNoOtherCalls();

            context.UnitOfWork.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task Send_MissingApartment_Should_ReturnNotFoundWithoutSaving()
        {
            // Arrange

            context.Apartments.Setup(repository => repository.GetByIdAsync(apartment.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Apartment?)null);

            // Act

            Result result = await context.Sender.Send(Command());

            // Assert

            result.IsFailure.Should().BeTrue();

            result.Error.Should().Be(ApartmentErrors.NotFound);

            context.UnitOfWork.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task Send_ConcurrencyFailure_Should_ReturnConflict()
        {
            // Arrange

            SetupExisting();

            context.UnitOfWork.Setup(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ConcurrencyException("Conflict", new InvalidOperationException()));

            // Act

            Result result = await context.Sender.Send(Command());

            // Assert

            result.IsFailure.Should().BeTrue();

            result.Error.Should().Be(ApartmentErrors.Conflict);

            context.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task Send_OtherSaveFailure_Should_Propagate(bool cancelled)
        {
            // Arrange

            SetupExisting();

            Exception failure = cancelled ? new OperationCanceledException() : new InvalidOperationException("Database unavailable");

            context.UnitOfWork.Setup(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(failure);

            // Act

            Func<Task> act = () => context.Sender.Send(Command());

            // Assert

            (await act.Should().ThrowAsync<Exception>()).Which.Should().BeSameAs(failure);
        }

        [TestMethod]
        [DataRow("id")]
        [DataRow("name")]
        [DataRow("nameLength")]
        [DataRow("description")]
        [DataRow("descriptionLength")]
        [DataRow("country")]
        [DataRow("state")]
        [DataRow("zip")]
        [DataRow("city")]
        [DataRow("street")]
        [DataRow("price")]
        [DataRow("cleaning")]
        [DataRow("currency")]
        [DataRow("amenities")]
        [DataRow("enum")]
        [DataRow("duplicates")]
        public async Task Send_InvalidInput_Should_RejectBeforeRepositoryAccess(string field)
        {
            // Arrange

            UpdateApartmentCommand valid = Command();

            UpdateApartmentCommand invalid = field switch
            {
                "id" => valid with { ApartmentId = Guid.Empty },
                "name" => valid with { Name = " " },
                "nameLength" => valid with { Name = new string('A', 201) },
                "description" => valid with { Description = "" },
                "descriptionLength" => valid with { Description = new string('A', 2001) },
                "country" => valid with { Country = "" },
                "state" => valid with { State = "" },
                "zip" => valid with { ZipCode = "" },
                "city" => valid with { City = "" },
                "street" => valid with { Street = "" },
                "price" => valid with { PriceAmount = 0 },
                "cleaning" => valid with { CleaningFeeAmount = -1 },
                "currency" => valid with { Currency = "GBP" },
                "amenities" => valid with { Amenities = null! },
                "enum" => valid with { Amenities = [(Amenity)999] },
                _ => valid with { Amenities = [Amenity.Wifi, Amenity.Wifi] }
            };

            // Act

            Func<Task> act = () => context.Sender.Send(invalid);

            // Assert

            await act.Should().ThrowAsync<ValidationException>();

            context.Apartments.VerifyNoOtherCalls();

            context.UnitOfWork.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task Handle_InvalidNameWithoutPipeline_Should_ReturnDomainError()
        {
            // Arrange

            UpdateApartmentCommandHandler handler = new(context.Apartments.Object, context.UnitOfWork.Object);

            // Act

            Result result = await handler.Handle(Command() with { Name = "" }, default);

            // Assert

            result.Error.Should().Be(NameErrors.Empty);

            context.Apartments.VerifyNoOtherCalls();

            context.UnitOfWork.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void Validator_Should_AcceptBoundaryValuesAndEmptyAmenities()
        {
            // Arrange

            UpdateApartmentCommand command = Command() with
            {
                Name = new string('A', 200),

                Description = new string('A', 2000),

                CleaningFeeAmount = 0,

                Amenities = []
            };

            // Act

            bool valid = new UpdateApartmentCommandValidator().Validate(command).IsValid;

            // Assert

            valid.Should().BeTrue();
        }

        private void SetupExisting() => context.Apartments
            .Setup(repository => repository.GetByIdAsync(apartment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(apartment);

        private UpdateApartmentCommand Command() => new(
            apartment.Id,
            "Updated apartment",
            "Updated description",
            "France",
            "Ile-de-France",
            "75001",
            "Paris",
            "Street 2",
            150m,
            30m,
            "USD",
            [Amenity.Wifi, Amenity.Parking]);
    }
}
