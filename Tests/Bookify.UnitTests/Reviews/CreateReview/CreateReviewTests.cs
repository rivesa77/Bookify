namespace Bookify.Application.Tests.Reviews.CreateReview
{
    using Bookify.Api.Controllers.Reviews;
    using Bookify.Application.Reviews.CreateReview;
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Apartments;
    using Bookify.Domain.Bookings;
    using Bookify.Domain.Commons;
    using Bookify.Domain.Reviews;
    using Bookify.Domain.Reviews.Events;
    using FluentAssertions;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public sealed class CreateReviewTests
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
        [DataRow(1)]
        [DataRow(5)]
        public async Task Send_CompletedBooking_Should_SaveReviewAndAccumulateEvent(int rating)
        {
            // Arrange
            using ReviewTestContext reviewTestContext = new();
            using CancellationTokenSource cancellation = new();

            Booking booking = CreateBooking(BookingStatus.Completed);
            Review? added = null;

            reviewTestContext.Bookings
                .Setup(r => r.GetByIdAsync(booking.Id, cancellation.Token))
                .ReturnsAsync(booking);

            reviewTestContext.Reviews
                .Setup(r => r.Add(It.IsAny<Review>()))
                .Callback<Review>(r => added = r);

            reviewTestContext.UnitOfWork
                .Setup(u => u.SaveChangesAsync(cancellation.Token))
                .ReturnsAsync(1);

            string comment = new('A', 200);

            // Act
            Result<Guid> result = await reviewTestContext.Sender.Send(
                new CreateReviewCommand(booking.Id, rating, comment),
                cancellation.Token);

            // Assert
            result.IsSuccess.Should().BeTrue();

            added.Should().NotBeNull();

            result.Value.Should().NotBeEmpty().And.Be(added!.Id);

            added.BookingId.Should().Be(booking.Id);

            added.ApartmentId.Should().Be(booking.ApartmentId);

            added.UserId.Should().Be(booking.UserId);

            added.Rating.Value.Should().Be(rating);

            added.Comment.Value.Should().Be(comment);

            added.CreatedOnUtc.Should().Be(UtcNow);

            added.GetDomainEvents().Should().ContainSingle()
                .Which.Should().Be(new ReviewCreatedDomainEvent(added.Id));

            reviewTestContext.Bookings.Verify(r => r.GetByIdAsync(booking.Id, cancellation.Token), Times.Once);

            reviewTestContext.Reviews.Verify(r => r.Add(added), Times.Once);

            reviewTestContext.UnitOfWork.Verify(u => u.SaveChangesAsync(cancellation.Token), Times.Once);

            reviewTestContext.Bookings.VerifyNoOtherCalls();

            reviewTestContext.Reviews.VerifyNoOtherCalls();

            reviewTestContext.UnitOfWork.VerifyNoOtherCalls();
        }

        [TestMethod]
        [DataRow(BookingStatus.Reserved)]
        [DataRow(BookingStatus.Confirmed)]
        [DataRow(BookingStatus.Rejected)]
        [DataRow(BookingStatus.Cancelled)]
        public async Task Send_IneligibleBooking_Should_NotWrite(BookingStatus status)
        {
            // Arrange
            using ReviewTestContext reviewTestContext = new();

            Booking booking = CreateBooking(status);

            reviewTestContext.Bookings
                .Setup(r => r.GetByIdAsync(booking.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(booking);

            // Act
            Result<Guid> result = await reviewTestContext.Sender.Send(
                new CreateReviewCommand(booking.Id, 4, "Good stay"));

            // Assert
            result.IsFailure.Should().BeTrue();

            result.Error.Should().Be(ReviewErrors.NotEligible);

            reviewTestContext.Reviews.VerifyNoOtherCalls();

            reviewTestContext.UnitOfWork.VerifyNoOtherCalls();
        }

        [TestMethod]
        [DataRow("id")]
        [DataRow("lowRating")]
        [DataRow("highRating")]
        [DataRow("comment")]
        [DataRow("emptyComment")]
        [DataRow("nullComment")]
        public async Task Send_InvalidInput_Should_NotAccessRepositories(string invalidField)
        {
            // Arrange
            using ReviewTestContext reviewTestContext = new();

            CreateReviewCommand valid = new(Guid.NewGuid(), 4, "Good stay");

            CreateReviewCommand command = invalidField switch
            {
                "id" => valid with { BookingId = Guid.Empty },
                "lowRating" => valid with { Rating = 0 },
                "highRating" => valid with { Rating = 6 },
                "comment" => valid with { Comment = new string('A', 201) },
                "emptyComment" => valid with { Comment = " " },
                _ => valid with { Comment = null! }
            };

            // Act
            Func<Task> act = () => reviewTestContext.Sender.Send(command);

            // Assert
            await act.Should().ThrowAsync<Exceptions.ValidationException>();

            reviewTestContext.Bookings.VerifyNoOtherCalls();

            reviewTestContext.Reviews.VerifyNoOtherCalls();

            reviewTestContext.UnitOfWork.VerifyNoOtherCalls();
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
            response.Should().BeOfType<NotFoundObjectResult>()
                .Which.Value.Should().Be(BookingErrors.NotFound);

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

        [TestMethod]
        public async Task Send_SaveFailure_Should_Propagate()
        {
            // Arrange
            using ReviewTestContext reviewTestContext = new();

            Booking booking = CreateBooking(BookingStatus.Completed);

            reviewTestContext.Bookings
                .Setup(r => r.GetByIdAsync(booking.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(booking);

            reviewTestContext.Reviews.Setup(r => r.Add(It.IsAny<Review>()));

            reviewTestContext.UnitOfWork
                .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Save failed"));

            // Act
            Func<Task> act = () => reviewTestContext.Sender.Send(
                new CreateReviewCommand(booking.Id, 4, "Good stay"));

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Save failed");
        }
    }
}
