namespace Bookify.Domain.Abstractions
{
    using System;

    public abstract class Entity
    {
        private readonly List<IDomainEvent> domainEvents = [];

        protected Entity()
        {
        }

        protected Entity(Guid id)
        {
            Id = id;
        }

        public Guid Id { get; init; }

        public IReadOnlyList<IDomainEvent> GetDomainEvents()
        {
            return [.. domainEvents];
        }

        public void ClearDomainEvent()
        {
            domainEvents.Clear();
        }

        protected void RaiseDomainEvent(IDomainEvent domainEvent)
        {
            domainEvents.Add(domainEvent);
        }
    }
}