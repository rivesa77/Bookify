namespace Bookify.Domain.Abstractions
{
    using System;

    internal abstract class Entity
    {
        protected Entity(Guid id)
        {
            Id = id;
        }

        public Guid Id { get; init; }
    }
}