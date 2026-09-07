namespace Bookify.Application.Bookings.GetBooking
{
    using System;

    public sealed class BookingResponse
    {
        public Guid Id { get; init; }

        public Guid UserId { get; init; }

        public Guid ApartmentId { get; init; }

        public int Status { get; init; }

        public string PriceCurrency { get; init; } = String.Empty;

        public decimal CleaningFeeAmount { get; init; }

        public decimal CleaningFeeCurrency { get; init; }

        public decimal AmenitiesUpChargeAmount { get; init; }

        public decimal AmenitiesUpChargeCurrency { get; init; }

        public decimal TotalPriceAmount { get; init; }

        public decimal TotalPriceCurrency { get; init; }

        public DateOnly Start { get; init; }

        public DateOnly End { get; init; }

        public DateTime CreatedOnUtc { get; init; }
    }
}