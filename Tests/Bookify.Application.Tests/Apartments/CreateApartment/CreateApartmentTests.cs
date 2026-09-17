namespace Bookify.Application.Tests.Apartments.CreateApartment
{
    using Bookify.Application.Apartments.CreateApartment;
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Apartments;
    using Bookify.Domain.Commons;
    using Bookify.TestUtilities.Constants;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("Application")]
    public sealed class CreateApartmentTests
    {
        private readonly Bookify.TestUtilities.Context.ApartmentTestContext context = new();

        [TestCleanup]
        public void Cleanup()
        {
            context.Dispose();
        }

        [TestMethod]
        [DataRow("")]
        [DataRow(" ")]
        [DataRow(null)]
        public async Task Handle_InvalidNameWithoutPipeline_Should_ReturnDomainError(string? name)
        {
            // Arrange
            CreateApartmentCommandHandler handler = new(context.Apartments.Object, context.UnitOfWork.Object);

            // Act

            Result<Guid> result = await handler.Handle(ValidCommand() with { Name = name! }, default);

            // Assert

            result.IsFailure.Should().BeTrue();

            result.Error.Should().Be(NameErrors.Empty);

            context.Apartments.VerifyNoOtherCalls();

            context.UnitOfWork.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task Send_Should_CopyAmenitiesInsteadOfSharingCommandList()
        {
            // Arrange
            CreateApartmentCommand command = ValidCommand() with { Amenities = [Amenity.Wifi] };

            Apartment? created = null;

            context.Apartments.Setup(r => r.Add(It.IsAny<Apartment>())).Callback<Apartment>(a => created = a);

            context.UnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act

            await context.Sender.Send(command);

            command.Amenities.Clear();

            // Assert

            created!.Amenities.Should().Equal(Amenity.Wifi);
        }

        private static CreateApartmentCommand ValidCommand() => new(
            ApartmentConstants.Name,
            ApartmentConstants.Description,
            ApartmentConstants.Country,
            ApartmentConstants.State,
            ApartmentConstants.ZipCode,
            ApartmentConstants.City,
            ApartmentConstants.Street,
            ApartmentConstants.PriceAmount,
            ApartmentConstants.CleaningFeeAmount,
            ApartmentConstants.Currency,
            ApartmentConstants.Amenities);

        [TestMethod]
        public async Task Send_Should_CreateApartmentAndSaveOnce()
        {
            // Arrange
            Address address = new(
                ApartmentConstants.Country,
                ApartmentConstants.State,
                ApartmentConstants.ZipCode,
                ApartmentConstants.City,
                ApartmentConstants.Street);

            Apartment apartment = new(
                Guid.NewGuid(),
                Name.Create(ApartmentConstants.Name).Value,
                new Description(ApartmentConstants.Description),
                address,
                new Money(ApartmentConstants.PriceAmount, Currency.Eur),
                new Money(ApartmentConstants.CleaningFeeAmount, Currency.Eur),
                ApartmentConstants.Amenities);

            Mock<IApartmentRepository> repository = context.Apartments;

            Mock<IUnitOfWork> unitOfWork = context.UnitOfWork;

            using CancellationTokenSource cancellation = new();

            Apartment? apartmentResult = null;

            repository
                .Setup(r => r.Add(It.IsAny<Apartment>()))
                .Callback<Apartment>(a => apartmentResult = a)
                .Verifiable(Times.Once);

            unitOfWork
                .Setup(u => u.SaveChangesAsync(cancellation.Token))
                .ReturnsAsync(1)
                .Verifiable(Times.Once);

            CreateApartmentCommand command = ValidCommand();

            // Act

            Result<Guid> result = await context.Sender.Send(command, cancellation.Token);

            // Assert

            result.IsSuccess
                .Should()
                .BeTrue();

            apartmentResult
                .Should()
                .NotBeNull();

            result.Value
                .Should()
                .NotBeEmpty();

            result.Value
                .Should()
                .Be(apartmentResult!.Id);

            apartment
                .Should()
                .BeEquivalentTo(apartmentResult, options => options.Excluding(a => a.Id));

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

            Mock<IApartmentRepository> repository = context.Apartments;

            Mock<IUnitOfWork> unitOfWork = context.UnitOfWork;

            // Act

            Func<Task> act = () => context.Sender.Send(command, default);

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

            context.Apartments.Setup(r => r.Add(It.IsAny<Apartment>()));

            context.UnitOfWork.Setup(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Database unavailable"));

            // Act

            Func<Task> act = () => context.Sender.Send(ValidCommand(), default);

            // Assert

            await act
                .Should()
                .ThrowAsync<InvalidOperationException>();
        }
    }
}