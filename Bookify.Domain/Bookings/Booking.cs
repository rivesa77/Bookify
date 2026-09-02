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
            DateTime utcNow)
        {
            PricingDetails pricingDetails = PricingServices.CalculatePricing(apartment, duration);

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
    }
}