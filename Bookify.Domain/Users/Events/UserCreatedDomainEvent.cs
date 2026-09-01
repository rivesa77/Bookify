namespace Bookify.Domain.Users.Events
{
    using Bookify.Domain.Abstractions;

    public sealed record UserCreatedDomainEvent(Guid UserId) : IDomainEvent
    {
    }
}