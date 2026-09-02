namespace Bookify.Domain.Bookings.Events
{
    using System;
    using Bookify.Domain.Abstractions;

    public sealed record BookingReservedDomainEvent(Guid BookingId) : IDomainEvent
    {
    }
}