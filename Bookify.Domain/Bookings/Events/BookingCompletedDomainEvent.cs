namespace Bookify.Domain.Bookings.Events
{
    using System;
    using Bookify.Domain.Abstractions;

    public sealed record BookingCompletedDomainEvent(Guid BookingId) : IDomainEvent
    {
    }
}