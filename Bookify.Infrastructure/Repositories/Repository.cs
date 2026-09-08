namespace Bookify.Infrastructure.Repositories
{
    using Bookify.Domain.Abstractions;
    using Microsoft.EntityFrameworkCore;

    internal abstract class Repository<TEntity>
        where TEntity : Entity
    {
        protected readonly ApplicationDbContext applicationDbContext;

        protected Repository(ApplicationDbContext applicationDbContext)
        {
            this.applicationDbContext = applicationDbContext;
        }

        public async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await applicationDbContext
                .Set<TEntity>()
                .FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        }

        public void Add(TEntity entity)
        {
            applicationDbContext.Add(entity);
        }
    }
}