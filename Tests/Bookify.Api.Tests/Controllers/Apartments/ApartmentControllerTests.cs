namespace Bookify.Api.Tests.Controllers.Apartment
{
    using System;
    using Bookify.Api.Controllers.Apartment;
    using Bookify.Application.Apartments.CreateApartment;
    using Bookify.Domain.Apartments;
    using Bookify.TestUtilities.Context;
    using FluentAssertions;
    using Microsoft.AspNetCore.Mvc;
    using Moq;

    [TestClass]
    [TestCategory("Controller")]
    public class ApartmentControllerTests
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
        public async Task Controller_Should_Return201WithPersistedId()
        {
            // Arrange
            using ApartmentTestContext apartmentTestContext = new();

            apartmentTestContext.Apartments.Setup(r => r.Add(It.IsAny<Apartment>()));

            apartmentTestContext.UnitOfWork
                .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            ApartmentsController controller = new(apartmentTestContext.Sender);

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