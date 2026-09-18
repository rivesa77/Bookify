namespace Bookify.Application.Tests.Reviews.CreateReview
{
    using Bookify.Application.Reviews.CreateReview;
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Apartments;
    using Bookify.Domain.Bookings;
    using Bookify.Domain.Commons;
    using Bookify.Domain.Reviews;
    using Bookify.Domain.Reviews.Events;
    using Bookify.TestUtilities.Constants;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("Application")]
    public sealed class CreateReviewTests
    {
        private const string ValidComment = "Good stay";

        private readonly Bookify.TestUtilities.Context.ReviewTestContext context = new();

        [TestCleanup]
        public void Cleanup()
        {
            context.Dispose();
        }

        private static readonly DateTime UtcNow = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public async Task Send_MissingBooking_Should_ReturnNotFoundWithoutWriting()
        {
            // Arrange
            Guid bookingId = Guid.NewGuid();

            context.Bookings.Setup(r => r.GetByIdAsync(bookingId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Booking?)null);

            // Act

            Result<Guid> result = await context.Sender.Send(new CreateReviewCommand(
                bookingId,
                4,
                ValidComment));

            // Assert

            result.IsFailure.Should().BeTrue();

            result.Error.Should().Be(BookingErrors.NotFound);

            context.Reviews.VerifyNoOtherCalls();

            context.UnitOfWork.VerifyNoOtherCalls();
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(6)]
        public async Task Handle_InvalidRatingWithoutPipeline_Should_ReturnDomainError(int rating)
        {
            // Arrange
            using Support.ApplicationTestContext context = new();

            Booking booking = CreateBooking(BookingStatus.Completed);

            context.Bookings.Setup(r => r.GetByIdAsync(booking.Id, It.IsAny<CancellationToken>())).ReturnsAsync(booking);

            CreateReviewCommandHandler handler = new(
                context.Bookings.Object,
                context.Reviews.Object,
                context.UnitOfWork.Object,
                context.Clock.Object);

            // Act

            Result<Guid> result = await handler.Handle(new CreateReviewCommand(
                booking.Id,
                rating,
                ValidComment), default);

            // Assert

            result.IsFailure.Should().BeTrue();

            result.Error.Should().Be(Rating.Invalid);

            context.Reviews.VerifyNoOtherCalls();

            context.UnitOfWork.VerifyNoOtherCalls();

            context.Clock.VerifyGet(c => c.UtcNow, Times.Never);
        }

        private static Booking CreateBooking(BookingStatus status)
        {
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

            Booking booking = Booking.Reserve(
                apartment,
                Guid.NewGuid(),
                DateRange.Create(new DateOnly(
                    2026,
                    10,
                    1), new DateOnly(
                    2026,
                    10,
                    5)),
                UtcNow,
                new PricingServices());

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

            using CancellationTokenSource cancellation = new();

            Booking booking = CreateBooking(BookingStatus.Completed);

            Review? review = null;

            context.Bookings
                .Setup(r => r.GetByIdAsync(booking.Id, cancellation.Token))
                .ReturnsAsync(booking);

            context.Reviews
                .Setup(r => r.Add(It.IsAny<Review>()))
                .Callback<Review>(r => review = r);

            context.UnitOfWork
                .Setup(u => u.SaveChangesAsync(cancellation.Token))
                .ReturnsAsync(1);

            string comment = new('A', 200);

            // Act

            Result<Guid> result = await context.Sender.Send(
            new CreateReviewCommand(
                booking.Id,
                rating,
                comment),
            cancellation.Token);

            // Assert

            result.IsSuccess.Should().BeTrue();

            review.Should().NotBeNull();

            result.Value.Should().NotBeEmpty().And.Be(review!.Id);

            review.BookingId.Should().Be(booking.Id);

            review.ApartmentId.Should().Be(booking.ApartmentId);

            review.UserId.Should().Be(booking.UserId);

            review.Rating.Value.Should().Be(rating);

            review.Comment.Value.Should().Be(comment);

            review.CreatedOnUtc.Should().Be(UtcNow);

            review.GetDomainEvents().Should().ContainSingle()
                .Which.Should().Be(new ReviewCreatedDomainEvent(review.Id));

            context.Bookings.Verify(r => r.GetByIdAsync(booking.Id, cancellation.Token), Times.Once);

            context.Reviews.Verify(r => r.Add(review), Times.Once);

            context.UnitOfWork.Verify(u => u.SaveChangesAsync(cancellation.Token), Times.Once);

            context.Bookings.VerifyNoOtherCalls();

            context.Reviews.VerifyNoOtherCalls();

            context.UnitOfWork.VerifyNoOtherCalls();
        }

        [TestMethod]
        [DataRow(BookingStatus.Reserved)]
        [DataRow(BookingStatus.Confirmed)]
        [DataRow(BookingStatus.Rejected)]
        [DataRow(BookingStatus.Cancelled)]
        public async Task Send_IneligibleBooking_Should_NotWrite(BookingStatus status)
        {
            // Arrange

            Booking booking = CreateBooking(status);

            context.Bookings
                .Setup(r => r.GetByIdAsync(booking.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(booking);

            // Act

            Result<Guid> result = await context.Sender.Send(
            new CreateReviewCommand(
                booking.Id,
                4,
                ValidComment),
            default);

            // Assert

            result.IsFailure.Should().BeTrue();

            result.Error.Should().Be(ReviewErrors.NotEligible);

            context.Reviews.VerifyNoOtherCalls();

            context.UnitOfWork.VerifyNoOtherCalls();
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

            CreateReviewCommand valid = new(
                Guid.NewGuid(),
                4,
                ValidComment);

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

            Func<Task> act = () => context.Sender.Send(command, default);

            // Assert

            await act.Should().ThrowAsync<Exceptions.ValidationException>();

            context.Bookings.VerifyNoOtherCalls();

            context.Reviews.VerifyNoOtherCalls();

            context.UnitOfWork.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task Send_SaveFailure_Should_Propagate()
        {
            // Arrange

            Booking booking = CreateBooking(BookingStatus.Completed);

            context.Bookings
                .Setup(r => r.GetByIdAsync(booking.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(booking);

            context.Reviews.Setup(r => r.Add(It.IsAny<Review>()));

            context.UnitOfWork
                .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Save failed"));

            // Act

            Func<Task> act = () => context.Sender.Send(
            new CreateReviewCommand(
                booking.Id,
                4,
                ValidComment),
            default);

            // Assert

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Save failed");
        }
    }
}