namespace Bookify.Domain.Abstractions
{
    using System;

    public interface IRepository<TEntity>
        where TEntity : Entity
    {
        Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        void Add(TEntity entity);
    }
}