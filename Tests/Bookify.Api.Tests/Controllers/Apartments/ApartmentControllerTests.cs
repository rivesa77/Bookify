namespace Bookify.Api.Tests.Controllers.Apartment
{
    using System;
    using Bookify.Api.Controllers.Apartment;
    using Bookify.Application.Apartments.SearchApartments;
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Apartments;
    using Bookify.Domain.Commons;
    using Bookify.TestUtilities.Constants;
    using Bookify.TestUtilities.Context;
    using FluentAssertions;
    using MediatR;
    using Microsoft.AspNetCore.Mvc;
    using Moq;

    [TestClass]
    [TestCategory("Controller")]
    public class ApartmentControllerTests
    {
        [TestMethod]
        public async Task Controller_Should_Return201WithPersistedId()
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
                [Amenity.Wifi, Amenity.Parking]);

            using ApartmentTestContext apartmentTestContext = new();

            apartmentTestContext.Apartments
            .Setup(r => r.Add(It.Is<Apartment>(a =>
                a.Name == apartment.Name &&
                a.Description == apartment.Description &&
                a.Address == apartment.Address &&
                a.Price == apartment.Price &&
                a.CleaningFeeAmount == apartment.CleaningFeeAmount &&
                a.Amenities.SequenceEqual(apartment.Amenities))))
                .Verifiable(Times.Once);

            apartmentTestContext.UnitOfWork
                .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1)
                .Verifiable(Times.Once);

            ApartmentsController controller = new(apartmentTestContext.Sender);

            CreateApartmentRequest request = new(
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
                [Amenity.Wifi, Amenity.Parking]);

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

            apartmentTestContext.Apartments.VerifyAll();
            apartmentTestContext.UnitOfWork.VerifyAll();

            apartmentTestContext.Apartments.VerifyNoOtherCalls();
            apartmentTestContext.UnitOfWork.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task Controller_Should_Return200WithSearchApartments()
        {
            // Arrange
            Mock<ISender> sender = new(MockBehavior.Strict);

            using CancellationTokenSource cancellation = new();

            IReadOnlyList<ApartmentResponse> apartments =
            [
                new ApartmentResponse
                {
                    Id = Guid.NewGuid(),
                    Name = ApartmentConstants.Name,
                    Description = ApartmentConstants.Description,
                    Price = ApartmentConstants.PriceAmount,
                    Currency = ApartmentConstants.Currency,
                    Address = new AddressResponse
                    {
                        Country = ApartmentConstants.Country,
                        State = ApartmentConstants.State,
                        ZipCode = ApartmentConstants.ZipCode,
                        City = ApartmentConstants.City,
                        Street = ApartmentConstants.Street
                    }
                }
            ];

            DateOnly startDate = new(2026, 1, 1);
            DateOnly endDate = new(2026, 1, 10);

            SearchApartmentsQuery searchApartmentsQuery = new(startDate, endDate);

            sender
                .Setup(s => s.Send(
                    searchApartmentsQuery,
                    cancellation.Token))
                .ReturnsAsync(Result.Success(apartments))
                .Verifiable(Times.Once);

            ApartmentsController controller = new(sender.Object);

            // Act
            IActionResult result = await controller.SearchApartments(
                startDate,
                endDate,
                cancellation.Token);

            // Assert
            OkObjectResult response = result
                .Should()
                .BeOfType<OkObjectResult>()
                .Subject;

            response.StatusCode
                .Should()
                .Be(200);

            response.Value
                .Should()
                .BeAssignableTo<IReadOnlyList<ApartmentResponse>>()
                .Which
                .Should()
                .BeEquivalentTo(apartments);

            sender.VerifyAll();
            sender.VerifyNoOtherCalls();
        }
    }
}