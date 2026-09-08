namespace Bookify.Infrastructure.Repositories
{
    using System;
    using Bookify.Domain.Apartments;
    using Bookify.Domain.Bookings;
    using Microsoft.EntityFrameworkCore;

    internal sealed class BookingRepository : Repository<Booking>, IBookingRepository
    {
        private static readonly BookingStatus[] ActiveBookingStatus =
            [
                BookingStatus.Reserved,
                BookingStatus.Confirmed,
                BookingStatus.Completed,
            ];

        public BookingRepository(ApplicationDbContext applicationDbContext) : base(applicationDbContext)
        {
        }

        public async Task<bool> IsOverlappingAsync(
            Apartment apartment,
            DateRange duration,
            CancellationToken cancellationToken = default)
        {
            return await applicationDbContext
                .Set<Booking>()
                .AnyAsync(
                    booking =>
                        booking.ApartmentId == apartment.Id &&
                        booking.Duration.Start <= duration.End &&
                        booking.Duration.End >= duration.Start &&
                        ActiveBookingStatus.Contains(booking.Status),
                        cancellationToken);
        }
    }
}