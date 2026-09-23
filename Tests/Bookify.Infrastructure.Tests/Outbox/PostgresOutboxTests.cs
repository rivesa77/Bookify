namespace Bookify.Infrastructure.Tests.Outbox
{
    using Bookify.Application.Abstractions.DateTimeProvider;
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Users;
    using Bookify.Domain.Users.Events;
    using Bookify.Infrastructure.Data;
    using Bookify.Infrastructure.Outbox;
    using Bookify.Infrastructure.Repositories;
    using Bookify.Infrastructure.Tests.Support;
    using FluentAssertions;
    using MediatR;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Storage;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Options;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Quartz;

    [TestClass]
    [TestCategory("PostgreSQL")]
    public sealed class PostgresOutboxTests : PostgresTestBase
    {
        private const int BatchSize = 2;

        private readonly Mock<IPublisher> publisher = new(MockBehavior.Strict);

        private static readonly JsonSerializerSettings SerializerSettings = new() { TypeNameHandling = TypeNameHandling.All };

        [TestMethod]
        public async Task SaveChanges_Should_PersistUserAndPendingEventTogether()
        {
            // Arrange
            User user = InfrastructureTestData.CreateUser();

            new UserRepository(Context).Add(user);

            // Act
            await Context.SaveChangesAsync();

            // Assert
            await using ApplicationDbContext verification = InfrastructureTestData.CreateContext(connectionString: ConnectionString);

            (await verification.Set<User>().AnyAsync(saved => saved.Id == user.Id)).Should().BeTrue();

            OutboxMessage message = await verification.Set<OutboxMessage>().SingleAsync();

            message.Type.Should().Be(nameof(UserCreatedDomainEvent));

            JsonConvert.DeserializeObject<IDomainEvent>(message.Content, SerializerSettings)
                .Should().Be(new UserCreatedDomainEvent(user.Id));

            message.OccurredOnUtc.Should().Be(InfrastructureTestData.UtcNow);

            message.ProcessedOnUtc.Should().BeNull();

            message.Error.Should().BeNull();
        }

        [TestMethod]
        public async Task SaveChanges_RolledBackTransaction_Should_PersistNeitherUserNorEvent()
        {
            // Arrange
            User user = InfrastructureTestData.CreateUser();

            new UserRepository(Context).Add(user);

            await using IDbContextTransaction transaction = await Context.Database.BeginTransactionAsync();

            // Act
            await Context.SaveChangesAsync();

            (await Context.Set<OutboxMessage>().CountAsync()).Should().Be(1);

            await transaction.RollbackAsync();

            // Assert
            await using ApplicationDbContext verification = InfrastructureTestData.CreateContext(connectionString: ConnectionString);

            (await verification.Set<User>().AnyAsync(saved => saved.Id == user.Id)).Should().BeFalse();

            (await verification.Set<OutboxMessage>().AnyAsync()).Should().BeFalse();
        }

        [TestMethod]
        public async Task Execute_Should_ProcessOldestBatchAndSkipPreviouslyProcessedMessages()
        {
            // Arrange
            UserCreatedDomainEvent first = new(Guid.NewGuid());

            UserCreatedDomainEvent second = new(Guid.NewGuid());

            UserCreatedDomainEvent third = new(Guid.NewGuid());

            Context.AddRange(
                CreateMessage(third, 2),
                CreateMessage(first, 0),
                CreateMessage(second, 1));

            await Context.SaveChangesAsync();

            List<IDomainEvent> published = [];

            publisher.Setup(source => source.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()))
                .Callback<IDomainEvent, CancellationToken>((notification, _) => published.Add(notification))
                .Returns(Task.CompletedTask);

            // Act
            await ExecuteJob();

            // Assert
            published.Should().Equal(first, second);

            (await Context.Set<OutboxMessage>().CountAsync(message => message.ProcessedOnUtc == null)).Should().Be(1);

            await ExecuteJob();

            await ExecuteJob();

            published.Should().Equal(
                first,
                second,
                third);

            List<OutboxMessage> messages = await Context.Set<OutboxMessage>().AsNoTracking().ToListAsync();

            messages.Should().OnlyContain(message => message.ProcessedOnUtc == InfrastructureTestData.UtcNow && message.Error == null);
        }

        [TestMethod]
        public async Task Execute_PublicationFailure_Should_PersistErrorWithoutAutomaticRetry()
        {
            // Arrange
            UserCreatedDomainEvent notification = new(Guid.NewGuid());

            Context.Add(CreateMessage(notification, 0));

            await Context.SaveChangesAsync();

            InvalidOperationException failure = new("Publication failed");

            publisher.Setup(source => source.Publish<IDomainEvent>(notification, It.IsAny<CancellationToken>()))
                .ThrowsAsync(failure);

            // Act
            await ExecuteJob();

            await ExecuteJob();

            // Assert
            OutboxMessage message = await Context.Set<OutboxMessage>().AsNoTracking().SingleAsync();

            message.ProcessedOnUtc.Should().Be(InfrastructureTestData.UtcNow);

            message.Error.Should().Be(failure.ToString());

            publisher.Verify(source => source.Publish<IDomainEvent>(notification, It.IsAny<CancellationToken>()), Times.Once);

            publisher.VerifyNoOtherCalls();
        }

        private static OutboxMessage CreateMessage(IDomainEvent notification, int minutes) => new(
            Guid.NewGuid(),
            InfrastructureTestData.UtcNow.AddMinutes(minutes),
            notification.GetType().Name,
            JsonConvert.SerializeObject(notification, SerializerSettings));

        private Task ExecuteJob()
        {
            Mock<IDateTimeProvider> clock = new(MockBehavior.Strict);

            clock.SetupGet(source => source.UtcNow).Returns(InfrastructureTestData.UtcNow);

            ProcessOutboxMessagesJob job = new(
                new SqlConnectionFactory(ConnectionString),
                publisher.Object,
                clock.Object,
                Options.Create(new OutboxOptions { BatchSize = BatchSize }),
                NullLogger<ProcessOutboxMessagesJob>.Instance);

            Mock<IJobExecutionContext> execution = new(MockBehavior.Strict);

            execution.SetupGet(context => context.CancellationToken).Returns(CancellationToken.None);

            return job.Execute(execution.Object);
        }
    }
}
