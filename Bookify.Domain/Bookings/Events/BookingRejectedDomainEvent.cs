namespace Bookify.Domain.Bookings.Events
{
    using System;
    using Bookify.Domain.Abstractions;

    public sealed record BookingRejectedDomainEvent(Guid BookingId) : IDomainEvent
    {
    }
}