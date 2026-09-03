namespace Bookify.Domain.Bookings
{
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Apartments;
    using Bookify.Domain.Bookings.Events;
    using Bookify.Domain.Commons;

    public sealed class Booking : Entity
    {
        private Booking(
            Guid id,
            Guid apartmentId,
            Guid userId,
            DateRange duration,
            Money priceForPeriod,
            Money cleaningFee,
            Money amenitiesUpChange,
            Money totalPrice,
            BookingStatus status,
            DateTime createdOnUtc,
            DateTime? confirmedOnUtc = default,
            DateTime? rejectedOnUtc = default,
            DateTime? completedOnUtc = default,
            DateTime? cancelledOnUtc = default) : base(id)
        {
            ApartmentId = apartmentId;
            UserId = userId;
            Duration = duration;
            PriceForPeriod = priceForPeriod;
            CleaningFee = cleaningFee;
            AmenitiesUpChange = amenitiesUpChange;
            TotalPrice = totalPrice;
            Status = status;
            CreatedOnUtc = createdOnUtc;
            ConfirmedOnUtc = confirmedOnUtc;
            RejectedOnUtc = rejectedOnUtc;
            CompletedOnUtc = completedOnUtc;
            CancelledOnUtc = cancelledOnUtc;
        }

        public Guid ApartmentId { get; private set; }

        public Guid UserId { get; private set; }

        public DateRange Duration { get; private set; }

        public Money PriceForPeriod { get; private set; }

        public Money CleaningFee { get; private set; }

        public Money AmenitiesUpChange { get; private set; }

        public Money TotalPrice { get; private set; }

        public BookingStatus Status { get; private set; }

        public DateTime CreatedOnUtc { get; private set; }

        public DateTime? ConfirmedOnUtc { get; private set; }

        public DateTime? RejectedOnUtc { get; private set; }

        public DateTime? CompletedOnUtc { get; private set; }

        public DateTime? CancelledOnUtc { get; private set; }

        public static Booking Reserve(
            Apartment apartment,
            Guid userId,
            DateRange duration,
            DateTime utcNow,
            PricingServices pricingServices)
        {
            PricingDetails pricingDetails = pricingServices.CalculatePricing(apartment, duration);

            Booking booking = new(
                Guid.NewGuid(),
                apartment.Id,
                userId,
                duration,
                pricingDetails.PriceForPeriod,
                pricingDetails.CleaningFee,
                pricingDetails.AmenitiesUpCharge,
                pricingDetails.TotalPrice,
                BookingStatus.Reserved,
                utcNow);

            booking.RaiseDomainEvent(new BookingReservedDomainEvent(booking.Id));

            apartment.LastBookedOnUTC = utcNow;

            return booking;
        }

        public Result Confirm(DateTime utcNow)
        {
            if (Status != BookingStatus.Reserved)
            {
                return Result.Failure(BookingErrors.NotReserved);
            }

            Status = BookingStatus.Confirmed;
            ConfirmedOnUtc = utcNow;

            RaiseDomainEvent(new BookingConfirmedDomainEvent(Id));

            return Result.Success();
        }

        public Result Reject(DateTime utcNow)
        {
            if (Status != BookingStatus.Reserved)
            {
                return Result.Failure(BookingErrors.NotReserved);
            }

            Status = BookingStatus.Rejected;
            RejectedOnUtc = utcNow;

            RaiseDomainEvent(new BookingRejectedDomainEvent(Id));

            return Result.Success();
        }

        public Result Complete(DateTime utcNow)
        {
            if (Status != BookingStatus.Reserved)
            {
                return Result.Failure(BookingErrors.NotReserved);
            }

            Status = BookingStatus.Completed;
            CompletedOnUtc = utcNow;

            RaiseDomainEvent(new BookingCompletedDomainEvent(Id));

            return Result.Success();
        }

        public Result Cancel(DateTime utcNow)
        {
            if (Status != BookingStatus.Confirmed)
            {
                return Result.Failure(BookingErrors.NotConfirmed);
            }

            DateOnly currentDate = DateOnly.FromDateTime(utcNow);

            if (currentDate > Duration.Start)
            {
                return Result.Failure(BookingErrors.AlreadyStarted);
            }

            Status = BookingStatus.Cancelled;
            CancelledOnUtc = utcNow;

            RaiseDomainEvent(new BookingCancelledDomainEvent(Id));

            return Result.Success();
        }
    }
}