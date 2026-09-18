namespace Bookify.Domain.Tests.Support
{
    using Bookify.Domain.Apartments;
    using Bookify.Domain.Bookings;
    using Bookify.Domain.Commons;
    using Bookify.Domain.Users;

    internal static class DomainTestData
    {
        internal static readonly DateTime UtcNow = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

        internal static readonly DateOnly StartDate = new(2026, 10, 1);

        internal static readonly Guid UserId = Guid.Parse("10000000-0000-0000-0000-000000000001");

        internal static Apartment CreateApartment(
            decimal price = 100m,
            decimal cleaningFee = 20m,
            List<Amenity>? amenities = null,
            Currency? currency = null)
        {
            Currency selectedCurrency = currency ?? Currency.Eur;

            return new Apartment(
                Guid.NewGuid(),
                Name.Create("Test apartment").Value,
                new Description("City apartment"),
                new Address(
                    "Spain",
                    "Madrid",
                    "28001",
                    "Madrid",
                    "Street 1"),
                new Money(price, selectedCurrency),
                new Money(cleaningFee, selectedCurrency),
                amenities ?? []);
        }

        internal static Booking CreateBooking(BookingStatus status = BookingStatus.Reserved)
        {
            Booking booking = Booking.Reserve(
                CreateApartment(),
                UserId,
                DateRange.Create(StartDate, StartDate.AddDays(4)),
                UtcNow,
                new PricingServices());

            if (status == BookingStatus.Rejected)
            {
                booking.Reject(UtcNow.AddMinutes(1));
            }

            if (status is BookingStatus.Confirmed or BookingStatus.Completed or BookingStatus.Cancelled)
            {
                booking.Confirm(UtcNow.AddMinutes(1));
            }

            if (status == BookingStatus.Completed)
            {
                booking.Complete(StartDate.AddDays(4).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            }

            if (status == BookingStatus.Cancelled)
            {
                booking.Cancel(UtcNow.AddMinutes(2));
            }

            return booking;
        }

        internal static User CreateUser() => User.Create(
            new FirstName("Ana"),
            new LastName("Garcia"),
            new Email("ana@example.com"));
    }
}