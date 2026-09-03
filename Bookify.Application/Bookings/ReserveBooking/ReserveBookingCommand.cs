namespace Bookify.Application.Bookings.ReserveBooking
{
    using Bookify.Application.Abstractions.Messaging;

    public record ReserveBookingCommand(
        Guid ApartmentId,
        Guid UserId,
        DateOnly StartDate,
        DateOnly EndDate) : ICommand<Guid>;
}