namespace Bookify.Infrastructure.Tests.Persistence
{
    using Bookify.Application.Exceptions;
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Users;
    using Bookify.Domain.Users.Events;
    using Bookify.Infrastructure.Outbox;
    using Bookify.Infrastructure.Tests.Support;
    using FluentAssertions;
    using MediatR;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;

    [TestClass]
    [TestCategory("Infrastructure")]
    public sealed class ApplicationDbContextTests
    {
        private readonly Mock<IPublisher> publisher = new(MockBehavior.Strict);

        private readonly SaveInterceptor interceptor = new();

        private readonly User user = InfrastructureTestData.CreateUser();

        private ApplicationDbContext context = null!;

        private static readonly JsonSerializerSettings SerializerSettings = new()
        {
            TypeNameHandling = TypeNameHandling.All
        };

        [TestInitialize]
        public void Initialize()
        {
            context = InfrastructureTestData.CreateContext(publisher.Object, interceptor);

            context.Add(user);
        }

        [TestCleanup]
        public void Cleanup() => context.Dispose();

        [TestMethod]
        public async Task SaveChanges_Should_StageOutboxBeforeSaveAndClearEventsOnce()
        {
            // Arrange
            using CancellationTokenSource cancellation = new();

            // Act
            int result = await context.SaveChangesAsync(cancellation.Token);

            OutboxMessage first = context.Set<OutboxMessage>().Local.Single();

            await context.SaveChangesAsync(cancellation.Token);

            // Assert
            result.Should().Be(SaveInterceptor.SavedCount);

            interceptor.Token.Should().Be(cancellation.Token);

            interceptor.SaveCompleted.Should().BeTrue();

            interceptor.OutboxCountAtSave.Should().Be(1);

            AssertPendingOutbox().Should().BeSameAs(first);
        }

        [TestMethod]
        public async Task SaveChanges_ConcurrencyFailure_Should_TranslateExceptionAndKeepPendingOutbox()
        {
            // Arrange
            DbUpdateConcurrencyException original = new("Conflict");

            interceptor.Failure = original;

            // Act
            Func<Task> act = () => context.SaveChangesAsync();

            // Assert
            (await act.Should().ThrowAsync<ConcurrencyException>()).Which.InnerException.Should().BeSameAs(original);

            AssertPendingOutbox();
        }

        [TestMethod]
        public async Task SaveChanges_OtherFailure_Should_PropagateAndKeepPendingOutbox()
        {
            // Arrange
            DbUpdateException original = new("Failure");

            interceptor.Failure = original;

            // Act
            Func<Task> act = () => context.SaveChangesAsync();

            // Assert
            (await act.Should().ThrowAsync<DbUpdateException>()).Which.Should().BeSameAs(original);

            AssertPendingOutbox();
        }

        [TestMethod]
        public async Task SaveChanges_PublisherFailure_Should_NotAffectOutboxStaging()
        {
            // Arrange
            InvalidOperationException original = new("Publish failed");

            publisher.Setup(p => p.Publish<IDomainEvent>(new UserCreatedDomainEvent(user.Id), It.IsAny<CancellationToken>()))
                .ThrowsAsync(original);

            // Act
            int result = await context.SaveChangesAsync();

            // Assert
            result.Should().Be(SaveInterceptor.SavedCount);

            interceptor.SaveCompleted.Should().BeTrue();

            AssertPendingOutbox();
        }

        [TestMethod]
        public async Task SaveChanges_Cancelled_Should_KeepPendingOutboxAndNotPublish()
        {
            // Arrange
            using CancellationTokenSource cancellation = new();

            cancellation.Cancel();

            // Act
            Func<Task> act = () => context.SaveChangesAsync(cancellation.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();

            AssertPendingOutbox();

            interceptor.SaveCompleted.Should().BeFalse();
        }

        [TestMethod]
        public async Task SaveChanges_RetryAfterFailure_Should_ReusePendingOutbox()
        {
            // Arrange
            interceptor.Failure = new DbUpdateException("Failure");

            Func<Task> firstSave = () => context.SaveChangesAsync();

            await firstSave.Should().ThrowAsync<DbUpdateException>();

            OutboxMessage pending = context.Set<OutboxMessage>().Local.Single();

            interceptor.Failure = null;

            // Act
            int result = await context.SaveChangesAsync();

            // Assert
            result.Should().Be(SaveInterceptor.SavedCount);

            AssertPendingOutbox().Should().BeSameAs(pending);
        }

        private OutboxMessage AssertPendingOutbox()
        {
            user.GetDomainEvents().Should().BeEmpty();

            OutboxMessage message = context.Set<OutboxMessage>().Local.Should().ContainSingle().Subject;

            message.Id.Should().NotBeEmpty();

            message.Type.Should().Be(nameof(UserCreatedDomainEvent));

            message.OccurredOnUtc.Should().Be(InfrastructureTestData.UtcNow);

            message.ProcessedOnUtc.Should().BeNull();

            message.Error.Should().BeNull();

            IDomainEvent? domainEvent = JsonConvert.DeserializeObject<IDomainEvent>(message.Content, SerializerSettings);

            domainEvent.Should().Be(new UserCreatedDomainEvent(user.Id));

            // The interceptor suppresses the database write; only tracking is verified here.
            context.Entry(message).State.Should().Be(EntityState.Added);

            publisher.VerifyNoOtherCalls();

            return message;
        }

        private sealed class SaveInterceptor : SaveChangesInterceptor
        {
            internal const int SavedCount = 7;

            internal Exception? Failure { get; set; }

            internal bool SaveCompleted { get; private set; }

            internal CancellationToken Token { get; private set; }

            internal int OutboxCountAtSave { get; private set; }

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
            {
                Token = cancellationToken;

                OutboxCountAtSave = eventData.Context!.ChangeTracker.Entries<OutboxMessage>().Count();

                cancellationToken.ThrowIfCancellationRequested();

                if (Failure is not null)
                {
                    throw Failure;
                }

                return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(SavedCount));
            }

            public override ValueTask<int> SavedChangesAsync(
                SaveChangesCompletedEventData eventData,
                int result,
                CancellationToken cancellationToken = default)
            {
                SaveCompleted = true;

                return ValueTask.FromResult(result);
            }
        }
    }
}
