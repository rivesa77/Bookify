namespace Bookify.Infrastructure.Tests.Persistence
{
    using Bookify.Application.Exceptions;
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Users;
    using Bookify.Domain.Users.Events;
    using Bookify.Infrastructure.Tests.Support;
    using FluentAssertions;
    using MediatR;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("Infrastructure")]
    public sealed class ApplicationDbContextTests
    {
        private readonly Mock<IPublisher> publisher = new(MockBehavior.Strict);

        private readonly SaveInterceptor interceptor = new();

        private readonly User user = InfrastructureTestData.CreateUser();

        private ApplicationDbContext context = null!;

        [TestInitialize]
        public void Initialize()
        {
            context = InfrastructureTestData.CreateContext(publisher.Object, interceptor);

            context.Add(user);
        }

        [TestCleanup]
        public void Cleanup() => context.Dispose();

        [TestMethod]
        public async Task SaveChanges_Should_PublishAfterSaveAndClearEventsOnce()
        {
            // Arrange
            using CancellationTokenSource cancellation = new();

            publisher.Setup(p => p.Publish<IDomainEvent>(new UserCreatedDomainEvent(user.Id), It.IsAny<CancellationToken>()))
                .Callback(() => interceptor.SaveCompleted.Should().BeTrue())
                .Returns(Task.CompletedTask);

            // Act
            int result = await context.SaveChangesAsync(cancellation.Token);

            await context.SaveChangesAsync(cancellation.Token);

            // Assert
            result.Should().Be(SaveInterceptor.SavedCount);

            interceptor.Token.Should().Be(cancellation.Token);

            user.GetDomainEvents().Should().BeEmpty();

            publisher.Verify(p => p.Publish<IDomainEvent>(new UserCreatedDomainEvent(user.Id), It.IsAny<CancellationToken>()), Times.Once);

            publisher.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task SaveChanges_ConcurrencyFailure_Should_TranslateExceptionAndKeepEvents()
        {
            // Arrange
            DbUpdateConcurrencyException original = new("Conflict");

            interceptor.Failure = original;

            // Act
            Func<Task> act = () => context.SaveChangesAsync();

            // Assert
            (await act.Should().ThrowAsync<ConcurrencyException>()).Which.InnerException.Should().BeSameAs(original);

            user.GetDomainEvents().Should().ContainSingle();

            publisher.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task SaveChanges_OtherFailure_Should_PropagateAndKeepEvents()
        {
            // Arrange
            DbUpdateException original = new("Failure");

            interceptor.Failure = original;

            // Act
            Func<Task> act = () => context.SaveChangesAsync();

            // Assert
            (await act.Should().ThrowAsync<DbUpdateException>()).Which.Should().BeSameAs(original);

            user.GetDomainEvents().Should().ContainSingle();

            publisher.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task SaveChanges_PublisherFailure_Should_PropagateAfterSave()
        {
            // Arrange
            InvalidOperationException original = new("Publish failed");

            publisher.Setup(p => p.Publish<IDomainEvent>(new UserCreatedDomainEvent(user.Id), It.IsAny<CancellationToken>()))
                .ThrowsAsync(original);

            // Act
            Func<Task> act = () => context.SaveChangesAsync();

            // Assert
            (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(original);

            interceptor.SaveCompleted.Should().BeTrue();

            user.GetDomainEvents().Should().BeEmpty();
        }

        [TestMethod]
        public async Task SaveChanges_Cancelled_Should_KeepEventsAndNotPublish()
        {
            // Arrange
            using CancellationTokenSource cancellation = new();

            cancellation.Cancel();

            // Act
            Func<Task> act = () => context.SaveChangesAsync(cancellation.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();

            user.GetDomainEvents().Should().ContainSingle();

            publisher.VerifyNoOtherCalls();
        }

        private sealed class SaveInterceptor : SaveChangesInterceptor
        {
            internal const int SavedCount = 7;

            internal Exception? Failure { get; set; }

            internal bool SaveCompleted { get; private set; }

            internal CancellationToken Token { get; private set; }

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
            {
                Token = cancellationToken;

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
