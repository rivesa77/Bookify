namespace Bookify.Domain.Tests.Reviews
{
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Bookings;
    using Bookify.Domain.Reviews;
    using Bookify.Domain.Reviews.Events;
    using Bookify.Domain.Tests.Support;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Domain")]
    public sealed class ReviewTests
    {
        private static readonly Rating Rating = Rating.Create(5).Value;

        private static readonly Comment Comment = new("Excellent stay");

        private static readonly DateTime CreatedOn = DomainTestData.StartDate.AddDays(5)
            .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        [TestMethod]
        public void Create_CompletedBooking_Should_CopyPropertiesAndRaiseEvent()
        {
            // Arrange
            Booking booking = DomainTestData.CreateBooking(BookingStatus.Completed);

            IReadOnlyList<IDomainEvent> bookingEvents = booking.GetDomainEvents();

            // Act
            Result<Review> result = CreateReview(booking);

            // Assert
            result.IsSuccess.Should().BeTrue();

            Review review = result.Value;

            review.Id.Should().NotBeEmpty();

            review.ApartmentId.Should().Be(booking.ApartmentId);

            review.BookingId.Should().Be(booking.Id);

            review.UserId.Should().Be(booking.UserId);

            review.Rating.Should().Be(Rating);

            review.Comment.Should().Be(Comment);

            review.CreatedOnUtc.Should().Be(CreatedOn);

            review.GetDomainEvents().Should().ContainSingle().Which.Should().Be(new ReviewCreatedDomainEvent(review.Id));

            booking.Status.Should().Be(BookingStatus.Completed);

            booking.GetDomainEvents().Should().Equal(bookingEvents);
        }

        [TestMethod]
        [DataRow(BookingStatus.Reserved)]
        [DataRow(BookingStatus.Confirmed)]
        [DataRow(BookingStatus.Rejected)]
        [DataRow(BookingStatus.Cancelled)]
        public void Create_IneligibleBooking_Should_ReturnErrorWithoutChangingBooking(BookingStatus status)
        {
            // Arrange
            Booking booking = DomainTestData.CreateBooking(status);

            IReadOnlyList<IDomainEvent> events = booking.GetDomainEvents();

            // Act
            Result<Review> result = CreateReview(booking);

            // Assert
            result.IsFailure.Should().BeTrue();

            result.Error.Should().Be(ReviewErrors.NotEligible);

            booking.Status.Should().Be(status);

            booking.GetDomainEvents().Should().Equal(events);
        }

        [TestMethod]
        public void Create_Should_AssignIndependentIdentifiers()
        {
            // Arrange
            Booking firstBooking = DomainTestData.CreateBooking(BookingStatus.Completed);

            Booking secondBooking = DomainTestData.CreateBooking(BookingStatus.Completed);

            // Act
            Review first = CreateReview(firstBooking).Value;

            Review second = CreateReview(secondBooking).Value;

            // Assert
            first.Id.Should().NotBe(second.Id);

            first.BookingId.Should().NotBe(second.BookingId);
        }

        private static Result<Review> CreateReview(Booking booking) => Review.Create(
            booking,
            Rating,
            Comment,
            CreatedOn);
    }
}
