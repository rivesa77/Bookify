namespace Bookify.Infrastructure
{
    using Bookify.Application.Abstractions.DateTimeProvider;
    using Bookify.Application.Exceptions;
    using Bookify.Domain.Abstractions;
    using Bookify.Infrastructure.Outbox;
    using MediatR;
    using Microsoft.EntityFrameworkCore;
    using Newtonsoft.Json;

    public sealed class ApplicationDbContext : DbContext, IUnitOfWork
    {
        private readonly IPublisher publisher;
        private readonly IDateTimeProvider dateTimeProvider;

        private static readonly JsonSerializerSettings JsonSerializerSettings = new()
        {
            TypeNameHandling = TypeNameHandling.All,
        };

        public ApplicationDbContext(
            DbContextOptions options,
            IPublisher publisher,
            IDateTimeProvider dateTimeProvider)
                : base(options)
        {
            this.publisher = publisher;
            this.dateTimeProvider = dateTimeProvider;
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
                AddDomainEventAsOutboxMessageAsync();

                int result = await base.SaveChangesAsync(cancellationToken);

                return result;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new ConcurrencyException("Concurrency exception occurred.", ex);
            }
        }

        private void AddDomainEventAsOutboxMessageAsync()
        {
            IReadOnlyList<OutboxMessage> domainEventsOutboxMessage = [.. ChangeTracker
                .Entries<Entity>()
                .Select(entry => entry.Entity)
                .SelectMany(entity =>
                {
                    IReadOnlyList<IDomainEvent> domainEvents = entity.GetDomainEvents();
                    entity.ClearDomainEvent();
                    return domainEvents;
                })
                .Select(domainEvent => new OutboxMessage(
                    Guid.NewGuid(),
                    dateTimeProvider.UtcNow,
                    domainEvent.GetType().Name,
                    JsonConvert.SerializeObject(domainEvent,JsonSerializerSettings)))];

            AddRange(domainEventsOutboxMessage);
        }
    }
}