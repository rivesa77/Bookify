namespace Bookify.Domain.Bookings
{
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Apartments;

    public interface IBookingRepository : IRepository<Booking>
    {
        Task<bool> IsOverlappingAsync(
            Apartment apartment,
            DateRange duration,
            CancellationToken cancellationToken = default);
    }
}