namespace Bookify.Domain.Reviews.Events
{
    using System;
    using Bookify.Domain.Abstractions;

    public sealed record ReviewCreatedDomainEvent(Guid ReviewId) : IDomainEvent
    {
    }
}