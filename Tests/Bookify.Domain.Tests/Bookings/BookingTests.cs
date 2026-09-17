namespace Bookify.Domain.Tests.Bookings
{
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Apartments;
    using Bookify.Domain.Bookings;
    using Bookify.Domain.Bookings.Events;
    using Bookify.Domain.Commons;
    using Bookify.Domain.Tests.Support;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Domain")]
    public sealed class BookingTests
    {
        private static readonly DateTime TransitionTime = DomainTestData.UtcNow.AddHours(1);

        private readonly PricingServices pricing = new();

        private readonly Apartment apartment = DomainTestData.CreateApartment(amenities: [Amenity.Parking]);

        private static readonly DateRange Period = DateRange.Create(DomainTestData.StartDate, DomainTestData.StartDate.AddDays(4));

        [TestMethod]
        public void Reserve_Should_SetIdentifiersPricesDatesAndEvent()
        {
            // Arrange
            Guid userId = DomainTestData.UserId;

            // Act
            Booking booking = Reserve(userId);

            // Assert
            booking.Id.Should().NotBeEmpty();

            booking.ApartmentId.Should().Be(apartment.Id);

            booking.UserId.Should().Be(userId);

            booking.Duration.Should().Be(Period);

            booking.Status.Should().Be(BookingStatus.Reserved);

            booking.CreatedOnUtc.Should().Be(DomainTestData.UtcNow);

            booking.ConfirmedOnUtc.Should().BeNull();

            booking.RejectedOnUtc.Should().BeNull();

            booking.CompletedOnUtc.Should().BeNull();

            booking.CancelledOnUtc.Should().BeNull();

            booking.PriceForPeriod.Should().Be(new Money(400m, Currency.Eur));

            booking.CleaningFee.Should().Be(new Money(20m, Currency.Eur));

            booking.AmenitiesUpChange.Should().Be(new Money(4m, Currency.Eur));

            booking.TotalPrice.Should().Be(new Money(424m, Currency.Eur));

            apartment.LastBookedOnUTC.Should().Be(DomainTestData.UtcNow);

            booking.GetDomainEvents().Should().ContainSingle().Which.Should().Be(new BookingReservedDomainEvent(booking.Id));
        }

        [TestMethod]
        public void Reserve_Should_CreateIndependentIdentifiersAndUpdateApartmentTimestamp()
        {
            // Arrange
            Booking first = Reserve(DomainTestData.UserId);

            DateTime nextTime = DomainTestData.UtcNow.AddMinutes(10);

            // Act
            Booking second = Booking.Reserve(
                apartment,
                DomainTestData.UserId,
                Period,
                nextTime,
                pricing);

            // Assert
            second.Id.Should().NotBe(first.Id);

            second.CreatedOnUtc.Should().Be(nextTime);

            first.CreatedOnUtc.Should().Be(DomainTestData.UtcNow);

            apartment.LastBookedOnUTC.Should().Be(nextTime);
        }

        [TestMethod]
        [DataRow(BookingStatus.Reserved, BookingStatus.Confirmed, true)]
        [DataRow(BookingStatus.Confirmed, BookingStatus.Confirmed, false)]
        [DataRow(BookingStatus.Rejected, BookingStatus.Confirmed, false)]
        [DataRow(BookingStatus.Cancelled, BookingStatus.Confirmed, false)]
        [DataRow(BookingStatus.Completed, BookingStatus.Confirmed, false)]
        [DataRow(BookingStatus.Reserved, BookingStatus.Rejected, true)]
        [DataRow(BookingStatus.Confirmed, BookingStatus.Rejected, false)]
        [DataRow(BookingStatus.Rejected, BookingStatus.Rejected, false)]
        [DataRow(BookingStatus.Cancelled, BookingStatus.Rejected, false)]
        [DataRow(BookingStatus.Completed, BookingStatus.Rejected, false)]
        [DataRow(BookingStatus.Reserved, BookingStatus.Completed, false)]
        [DataRow(BookingStatus.Confirmed, BookingStatus.Completed, true)]
        [DataRow(BookingStatus.Rejected, BookingStatus.Completed, false)]
        [DataRow(BookingStatus.Cancelled, BookingStatus.Completed, false)]
        [DataRow(BookingStatus.Completed, BookingStatus.Completed, false)]
        [DataRow(BookingStatus.Reserved, BookingStatus.Cancelled, false)]
        [DataRow(BookingStatus.Confirmed, BookingStatus.Cancelled, true)]
        [DataRow(BookingStatus.Rejected, BookingStatus.Cancelled, false)]
        [DataRow(BookingStatus.Cancelled, BookingStatus.Cancelled, false)]
        [DataRow(BookingStatus.Completed, BookingStatus.Cancelled, false)]
        public void Transition_Should_EnforceStateMachine(
            BookingStatus initial,
            BookingStatus target,
            bool allowed)
        {
            // Arrange
            Booking booking = DomainTestData.CreateBooking(initial);

            DateTime? confirmed = booking.ConfirmedOnUtc;

            DateTime? rejected = booking.RejectedOnUtc;

            DateTime? completed = booking.CompletedOnUtc;

            DateTime? cancelled = booking.CancelledOnUtc;

            IReadOnlyList<IDomainEvent> previousEvents = booking.GetDomainEvents();

            // Act
            Result result = Transition(
                booking,
                target,
                TransitionTime);

            // Assert
            result.IsSuccess.Should().Be(allowed);

            booking.Status.Should().Be(allowed ? target : initial);

            booking.ConfirmedOnUtc.Should().Be(allowed && target == BookingStatus.Confirmed ? TransitionTime : confirmed);

            booking.RejectedOnUtc.Should().Be(allowed && target == BookingStatus.Rejected ? TransitionTime : rejected);

            booking.CompletedOnUtc.Should().Be(allowed && target == BookingStatus.Completed ? TransitionTime : completed);

            booking.CancelledOnUtc.Should().Be(allowed && target == BookingStatus.Cancelled ? TransitionTime : cancelled);

            if (allowed)
            {
                result.Error.Should().Be(Error.None);

                booking.GetDomainEvents().Should().Equal(previousEvents.Append(ExpectedEvent(target, booking.Id)));
            }
            else
            {
                Error expected = target is BookingStatus.Confirmed or BookingStatus.Rejected
                    ? BookingErrors.NotReserved
                    : BookingErrors.NotConfirmed;

                result.Error.Should().Be(expected);

                booking.GetDomainEvents().Should().Equal(previousEvents);
            }
        }

        [TestMethod]
        [DataRow(-1, true)]
        [DataRow(0, true)]
        [DataRow(1, false)]
        public void Cancel_Should_AllowUntilStartDateInclusive(int daysFromStart, bool allowed)
        {
            // Arrange
            Booking booking = DomainTestData.CreateBooking(BookingStatus.Confirmed);

            DateTime cancellationTime = DomainTestData.StartDate.AddDays(daysFromStart)
                .ToDateTime(new TimeOnly(23, 59), DateTimeKind.Utc);

            booking.ClearDomainEvent();

            // Act
            Result result = booking.Cancel(cancellationTime);

            // Assert
            result.IsSuccess.Should().Be(allowed);

            if (allowed)
            {
                booking.Status.Should().Be(BookingStatus.Cancelled);

                booking.CancelledOnUtc.Should().Be(cancellationTime);

                booking.GetDomainEvents().Should().ContainSingle().Which.Should().Be(new BookingCancelledDomainEvent(booking.Id));
            }
            else
            {
                result.Error.Should().Be(BookingErrors.AlreadyStarted);

                booking.Status.Should().Be(BookingStatus.Confirmed);

                booking.CancelledOnUtc.Should().BeNull();

                booking.GetDomainEvents().Should().BeEmpty();
            }
        }

        [TestMethod]
        public void SuccessfulLifecycle_Should_KeepEventsInOrder()
        {
            // Arrange
            Booking booking = Reserve(DomainTestData.UserId);

            DateTime completedOn = Period.End.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

            // Act
            Result confirmed = booking.Confirm(TransitionTime);

            Result completed = booking.Complete(completedOn);

            // Assert
            confirmed.IsSuccess.Should().BeTrue();

            completed.IsSuccess.Should().BeTrue();

            booking.ConfirmedOnUtc.Should().Be(TransitionTime);

            booking.CompletedOnUtc.Should().Be(completedOn);

            booking.GetDomainEvents().Should().Equal(
                new BookingReservedDomainEvent(booking.Id),
                new BookingConfirmedDomainEvent(booking.Id),
                new BookingCompletedDomainEvent(booking.Id));
        }

        private Booking Reserve(Guid userId) => Booking.Reserve(
            apartment,
            userId,
            Period,
            DomainTestData.UtcNow,
            pricing);

        private static Result Transition(
            Booking booking,
            BookingStatus target,
            DateTime time) => target switch
            {
                BookingStatus.Confirmed => booking.Confirm(time),

                BookingStatus.Rejected => booking.Reject(time),

                BookingStatus.Completed => booking.Complete(time),

                BookingStatus.Cancelled => booking.Cancel(time),

                _ => throw new ArgumentOutOfRangeException(nameof(target))
            };

        private static IDomainEvent ExpectedEvent(BookingStatus target, Guid id) => target switch
        {
            BookingStatus.Confirmed => new BookingConfirmedDomainEvent(id),

            BookingStatus.Rejected => new BookingRejectedDomainEvent(id),

            BookingStatus.Completed => new BookingCompletedDomainEvent(id),

            BookingStatus.Cancelled => new BookingCancelledDomainEvent(id),

            _ => throw new ArgumentOutOfRangeException(nameof(target))
        };
    }
}
