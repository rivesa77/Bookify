namespace Bookify.Application.Tests.Apartments.CreateApartment
{
    using Bookify.Api.Controllers.Apartment;
    using Bookify.Application;
    using Bookify.Application.Apartments.CreateApartment;
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Apartments;
    using FluentAssertions;
    using MediatR;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
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

        private static ServiceProvider BuildServices(
            Mock<IApartmentRepository> repository,
            Mock<IUnitOfWork> unitOfWork)
        {
            ServiceCollection services = new();
            services.AddLogging();
            services.AddApplication();

            services.AddSingleton(repository.Object);
            services.AddSingleton(unitOfWork.Object);

            return services.BuildServiceProvider();
        }

        [TestMethod]
        public async Task Send_Should_CreateApartmentAndSaveOnce()
        {
            // Arrange
            Mock<IApartmentRepository> repository = new(MockBehavior.Strict);

            Mock<IUnitOfWork> unitOfWork = new(MockBehavior.Strict);

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

            using ServiceProvider services = BuildServices(repository, unitOfWork);

            CreateApartmentCommand command = ValidCommand();

            // Act
            Result<Guid> result = await services.GetRequiredService<ISender>().Send(command, cancellation.Token);

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
                "name" => valid with { Name = "Too short" },
                "description" => valid with { Description = new string('A', 2001) },
                "address" => valid with { Street = " " },
                "price" => valid with { PriceAmount = 0 },
                "cleaning" => valid with { CleaningFeeAmount = -1 },
                "currency" => valid with { Currency = "GBP" },
                "amenity" => valid with { Amenities = [(Amenity)999] },
                "duplicate" => valid with { Amenities = [Amenity.Wifi, Amenity.Wifi] },
                _ => valid with { Amenities = null! }
            };

            Mock<IApartmentRepository> repository = new(MockBehavior.Strict);

            Mock<IUnitOfWork> unitOfWork = new(MockBehavior.Strict);

            using ServiceProvider services = BuildServices(repository, unitOfWork);

            // Act
            Func<Task> act = () => services.GetRequiredService<ISender>().Send(command);

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
            Mock<IApartmentRepository> repository = new();
            Mock<IUnitOfWork> unitOfWork = new();

            unitOfWork.Setup(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Database unavailable"));

            // Act
            using ServiceProvider services = BuildServices(repository, unitOfWork);

            Func<Task> act = () => services.GetRequiredService<ISender>().Send(ValidCommand());

            // Assert
            await act
                .Should()
                .ThrowAsync<InvalidOperationException>();
        }

        [TestMethod]
        public async Task Controller_Should_Return201WithPersistedId()
        {
            // Arrange
            Mock<IApartmentRepository> repository = new();

            Mock<IUnitOfWork> unitOfWork = new();

            unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            using ServiceProvider services = BuildServices(repository, unitOfWork);

            ApartmentsController controller = new(services.GetRequiredService<ISender>());

            CreateApartmentCommand command = ValidCommand();

            CreateApartmentRequest request = new(
                command.Name,
                command.Description,
                command.Country,
                command.State,
                command.ZipCode,
                command.City,
                command.Street,
                command.PriceAmount,
                command.CleaningFeeAmount,
                command.Currency,
                command.Amenities);

            // Act
            IActionResult result = await controller.CreateApartment(request, CancellationToken.None);

            // Assert
            ObjectResult response = result
                .Should()
                .BeAssignableTo<ObjectResult>()
                .Subject;

            response.StatusCode
                .Should()
                .Be(201);

            response.Value
                .Should()
                .BeOfType<Guid>()
                .Which
                .Should()
                .NotBeEmpty();
        }
    }
}