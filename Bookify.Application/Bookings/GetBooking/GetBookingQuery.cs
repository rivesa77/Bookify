namespace Bookify.Application.Bookings.GetBooking
{
    using System;
    using Bookify.Application.Abstractions.Messaging;

    public sealed record GetBookingQuery(Guid BookingId) : IQuery<BookingResponse>
    {
    }
}