namespace Bookify.Infrastructure
{
    using Bookify.Domain.Abstractions;
    using MediatR;
    using Microsoft.EntityFrameworkCore;

    public sealed class ApplicationDbContext : DbContext, IUnitOfWork
    {
        private readonly IPublisher publisher;

        public ApplicationDbContext(DbContextOptions options, IPublisher publisher)
            : base(options)
        {
            this.publisher = publisher;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

            base.OnModelCreating(modelBuilder);
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            try
            {
                int result = await base.SaveChangesAsync(cancellationToken);

                await PublishDomainEventAsync();

                return result;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new DbUpdateConcurrencyException("Concurrency exception occurred.", ex);
            }
        }

        private async Task PublishDomainEventAsync()
        {
            IReadOnlyList<IDomainEvent> domainEvents = [.. ChangeTracker
                .Entries<Entity>()
                .Select(entry => entry.Entity)
                .SelectMany(entity =>
                {
                    IReadOnlyList<IDomainEvent> domainEvents = entity.GetDomainEvents();

                    entity.ClearDomainEvent();

                    return domainEvents;
                })];

            foreach (IDomainEvent domainEvent in domainEvents)
            {
                await publisher.Publish(domainEvent);
            }
        }
    }
}