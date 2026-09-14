namespace Bookify.Api.Tests.Controllers.Reviews
{
    using System;
    using Bookify.Api.Controllers.Reviews;
    using Bookify.Domain.Apartments;
    using Bookify.Domain.Bookings;
    using Bookify.Domain.Commons;
    using Bookify.Domain.Reviews;
    using Bookify.TestUtilities.Context;
    using FluentAssertions;
    using Microsoft.AspNetCore.Mvc;
    using Moq;

    [TestClass]
    [TestCategory("Controller")]
    public class ReviewControllerTests
    {
        private static readonly DateTime UtcNow = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

        private static Booking CreateBooking(BookingStatus status)
        {
            Apartment apartment = new(
                Guid.NewGuid(),
                Name.Create(new string('A', Name.ExactLength)).Value,
                new Description("Description"),
                new Address("Spain", "Madrid", "28001", "Madrid", "Street"),
                new Money(100, Currency.Eur),
                Money.Zero(Currency.Eur),
                []);

            Booking booking = Booking.Reserve(
                apartment,
                Guid.NewGuid(),
                DateRange.Create(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 5)),
                UtcNow, new PricingServices());

            if (status == BookingStatus.Rejected)
            {
                booking.Reject(UtcNow).IsSuccess.Should().BeTrue();
            }
            else if (status != BookingStatus.Reserved)
            {
                booking.Confirm(UtcNow).IsSuccess
                    .Should()
                    .BeTrue();

                if (status == BookingStatus.Completed)
                {
                    booking.Complete(UtcNow).IsSuccess
                        .Should()
                        .BeTrue();
                }
                else if (status == BookingStatus.Cancelled)
                {
                    booking.Cancel(UtcNow).IsSuccess
                        .Should()
                        .BeTrue();
                }
            }

            booking.ClearDomainEvent();

            return booking;
        }

        [TestMethod]
        public async Task Controller_MissingBooking_Should_Return404WithoutSaving()
        {
            // Arrange
            using ReviewTestContext reviewTestContext = new();

            Guid bookingId = Guid.NewGuid();

            reviewTestContext.Bookings
                .Setup(r => r.GetByIdAsync(bookingId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Booking?)null);

            ReviewsController controller = new(reviewTestContext.Sender);

            // Act
            IActionResult response = await controller.CreateReview(
                new(bookingId, 4, "Good stay"),
                CancellationToken.None);

            // Assert
            response
                .Should()
                .BeOfType<NotFoundObjectResult>()
                .Which.Value
                .Should()
                .Be(BookingErrors.NotFound);

            reviewTestContext.Reviews.VerifyNoOtherCalls();

            reviewTestContext.UnitOfWork.VerifyNoOtherCalls();
        }

        [TestMethod]
        [DataRow(BookingStatus.Completed, 201)]
        [DataRow(BookingStatus.Reserved, 400)]
        public async Task Controller_Should_ReturnStatusForBookingState(BookingStatus status, int expectedStatus)
        {
            // Arrange
            using ReviewTestContext reviewTestContext = new();

            Booking booking = CreateBooking(status);

            reviewTestContext.Bookings
                .Setup(r => r.GetByIdAsync(booking.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(booking);

            if (status == BookingStatus.Completed)
            {
                reviewTestContext.Reviews.Setup(r => r.Add(It.IsAny<Review>()));

                reviewTestContext.UnitOfWork
                    .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(1);
            }

            ReviewsController controller = new(reviewTestContext.Sender);

            // Act
            IActionResult result = await controller.CreateReview(
                new(booking.Id, 4, "Good stay"),
                CancellationToken.None);

            // Assert
            ObjectResult response = result.Should().BeAssignableTo<ObjectResult>().Subject;

            response.StatusCode.Should().Be(expectedStatus);

            if (expectedStatus == 201)
            {
                response.Value.Should().BeOfType<Guid>().Which.Should().NotBeEmpty();
            }
            else
            {
                response.Value.Should().Be(ReviewErrors.NotEligible);
            }
        }
    }
}