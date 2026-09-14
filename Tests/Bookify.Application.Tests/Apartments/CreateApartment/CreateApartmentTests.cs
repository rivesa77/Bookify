namespace Bookify.Application.Tests.Apartments.CreateApartment
{
    using Bookify.Application.Apartments.CreateApartment;
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Apartments;
    using Bookify.TestUtilities.Context;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("Application")]
    public sealed class CreateApartmentTests
    {
        private static CreateApartmentCommand ValidCommand() => new(
            new string('A', Name.ExactLength),
            "Apartment description",
            "Spain",
            "Madrid",
            "28001",
            "Madrid",
            "Street 1",
            100m,
            25m,
            "EUR",
            [Amenity.Wifi, Amenity.Parking]);

        [TestMethod]
        public async Task Send_Should_CreateApartmentAndSaveOnce()
        {
            // Arrange
            using ApartmentTestContext apartmentTestContext = new();

            Mock<IApartmentRepository> repository = apartmentTestContext.Apartments;

            Mock<IUnitOfWork> unitOfWork = apartmentTestContext.UnitOfWork;

            using CancellationTokenSource cancellation = new();

            Apartment? apartment = null;

            repository.Setup(r =>
                r.Add(It.IsAny<Apartment>()))
                .Callback<Apartment>(a => apartment = a)
                .Verifiable(Times.Once);

            unitOfWork.Setup(
                u => u.SaveChangesAsync(cancellation.Token))
                .ReturnsAsync(1)
                .Verifiable(Times.Once);

            CreateApartmentCommand command = ValidCommand();

            // Act
            Result<Guid> result = await apartmentTestContext.Sender.Send(command, cancellation.Token);

            // Assert
            result.IsSuccess.Should().BeTrue();
            apartment.Should().NotBeNull();
            result.Value.Should().NotBeEmpty();
            result.Value.Should().Be(apartment!.Id);
            apartment.Name.Value.Should().Be(command.Name);
            apartment.Description.Value.Should().Be(command.Description);
            apartment.Address.Should().Be(new Address("Spain", "Madrid", "28001", "Madrid", "Street 1"));
            apartment.Price.Amount.Should().Be(100m);
            apartment.CleaningFeeAmount.Amount.Should().Be(25m);
            apartment.Price.Currency.Code.Should().Be("EUR");
            apartment.CleaningFeeAmount.Currency.Should().Be(apartment.Price.Currency);
            apartment.LastBookedOnUTC.Should().BeNull();
            apartment.Amenities.Should().Equal(command.Amenities);
            command.Amenities.Clear();
            apartment.Amenities.Should().HaveCount(2);

            repository.VerifyAll();
            unitOfWork.VerifyAll();

            repository.VerifyNoOtherCalls();
            unitOfWork.VerifyNoOtherCalls();
        }

        [TestMethod]
        [DataRow("name")]
        [DataRow("description")]
        [DataRow("address")]
        [DataRow("price")]
        [DataRow("cleaning")]
        [DataRow("currency")]
        [DataRow("amenity")]
        [DataRow("duplicate")]
        [DataRow("nullAmenities")]
        public async Task Send_InvalidInput_Should_NotWrite(string invalidField)
        {
            // Arrange
            CreateApartmentCommand valid = ValidCommand();

            CreateApartmentCommand command = invalidField switch
            {
                "name" => valid with { Name = new string('A', 201) },
                "description" => valid with { Description = new string('A', 2001) },
                "address" => valid with { Street = " " },
                "price" => valid with { PriceAmount = 0 },
                "cleaning" => valid with { CleaningFeeAmount = -1 },
                "currency" => valid with { Currency = "GBP" },
                "amenity" => valid with { Amenities = [(Amenity)999] },
                "duplicate" => valid with { Amenities = [Amenity.Wifi, Amenity.Wifi] },
                _ => valid with { Amenities = null! }
            };

            using ApartmentTestContext apartmentTestContext = new();

            Mock<IApartmentRepository> repository = apartmentTestContext.Apartments;

            Mock<IUnitOfWork> unitOfWork = apartmentTestContext.UnitOfWork;

            // Act
            Func<Task> act = () => apartmentTestContext.Sender.Send(command, default);

            // Assert
            await act
                .Should()
                .ThrowAsync<Exceptions.ValidationException>();

            repository.VerifyNoOtherCalls();
            unitOfWork.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void Validator_Should_AcceptZeroCleaningAndNoAmenities()
        {
            // Arrange & Act
            CreateApartmentCommand command = ValidCommand() with
            {
                CleaningFeeAmount = 0,
                Currency = "USD",
                Amenities = []
            };

            // Assert
            new CreateApartmentCommandValidator()
                .Validate(command).IsValid
                .Should()
                .BeTrue();
        }

        [TestMethod]
        public async Task Send_SaveFailure_Should_Propagate()
        {
            // Arrange
            using ApartmentTestContext apartmentTestContext = new();

            apartmentTestContext.Apartments.Setup(r => r.Add(It.IsAny<Apartment>()));

            apartmentTestContext.UnitOfWork.Setup(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Database unavailable"));

            // Act
            Func<Task> act = () => apartmentTestContext.Sender.Send(ValidCommand(), default);

            // Assert
            await act
                .Should()
                .ThrowAsync<InvalidOperationException>();
        }
    }
}