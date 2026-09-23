namespace Bookify.Infrastructure.Tests.Outbox
{
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Users.Events;
    using Bookify.Infrastructure.Tests.Support;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("Infrastructure")]
    public sealed class ProcessOutboxMessagesJobTests
    {
        private readonly OutboxJobTestContext context = new();

        private readonly UserCreatedDomainEvent domainEvent = new(Guid.NewGuid());

        [TestCleanup]
        public void Cleanup() => context.Dispose();

        [TestMethod]
        public async Task Execute_EmptyBatch_Should_CommitWithoutPublishing()
        {
            // Arrange
            // The query double starts with no rows.

            // Act
            await context.Execute();

            // Assert
            context.Query.Should().Contain("WHERE processed_on_utc IS NULL")
                .And.Contain("ORDER BY occurred_on_utc")
                .And.Contain($"LIMIT {OutboxJobTestContext.BatchSize}")
                .And.Contain("FOR UPDATE");

            context.Updates.Should().BeEmpty();

            context.Operations.Should().Equal("read", "commit");

            AssertCommittedAndDisposed();
        }

        [TestMethod]
        public async Task Execute_ValidMessage_Should_PublishBeforeUpdatingAndCommit()
        {
            // Arrange
            Guid id = context.Add(domainEvent);

            using CancellationTokenSource cancellation = new();

            context.Publisher.Setup(publisher => publisher.Publish<IDomainEvent>(domainEvent, cancellation.Token))
                .Callback(() => context.Operations.Add("publish"))
                .Returns(Task.CompletedTask);

            // Act
            await context.Execute(cancellation.Token);

            // Assert
            Dictionary<string, object?> update = context.Updates.Should().ContainSingle().Subject;

            update["Id"].Should().Be(id);

            update["ProcessedOnUtc"].Should().Be(InfrastructureTestData.UtcNow);

            update["Error"].Should().BeNull();

            context.Operations.Should().Equal(
                "read",
                "publish",
                "update",
                "commit");

            context.Publisher.Verify(publisher => publisher.Publish<IDomainEvent>(domainEvent, cancellation.Token), Times.Once);

            AssertCommittedAndDisposed();
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task Execute_PublicationFailure_Should_RecordErrorAndContinue(bool cancelled)
        {
            // Arrange
            Guid failedId = context.Add(domainEvent);

            UserCreatedDomainEvent next = new(Guid.NewGuid());

            Guid nextId = context.Add(next);

            Exception failure = cancelled ? new OperationCanceledException("Cancelled") : new InvalidOperationException("Publish failed");

            context.Publisher.Setup(publisher => publisher.Publish<IDomainEvent>(domainEvent, It.IsAny<CancellationToken>()))
                .ThrowsAsync(failure);

            context.Publisher.Setup(publisher => publisher.Publish<IDomainEvent>(next, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await context.Execute();

            // Assert
            context.Updates.Should().HaveCount(2);

            context.Updates[0]["Id"].Should().Be(failedId);

            context.Updates[0]["Error"].Should().Be(failure.ToString());

            context.Updates[0]["ProcessedOnUtc"].Should().Be(InfrastructureTestData.UtcNow);

            context.Updates[1]["Id"].Should().Be(nextId);

            context.Updates[1]["Error"].Should().BeNull();

            context.Publisher.Verify(publisher => publisher.Publish<IDomainEvent>(domainEvent, It.IsAny<CancellationToken>()), Times.Once);

            context.Publisher.Verify(publisher => publisher.Publish<IDomainEvent>(next, It.IsAny<CancellationToken>()), Times.Once);

            AssertCommittedAndDisposed();
        }

        [TestMethod]
        public async Task Execute_InvalidJson_Should_RecordErrorWithoutPublishing()
        {
            // Arrange
            Guid id = context.AddContent("{invalid");

            // Act
            await context.Execute();

            // Assert
            Dictionary<string, object?> update = context.Updates.Should().ContainSingle().Subject;

            update["Id"].Should().Be(id);

            update["Error"].Should().BeOfType<string>().Which.Should().Contain("JsonReaderException");

            update["ProcessedOnUtc"].Should().Be(InfrastructureTestData.UtcNow);

            AssertCommittedAndDisposed();
        }

        [TestMethod]
        [DataRow("read")]
        [DataRow("update")]
        [DataRow("commit")]
        public async Task Execute_DatabaseFailure_Should_PropagateAndDispose(string operation)
        {
            // Arrange
            InvalidOperationException failure = new("Database failure");

            context.Add(domainEvent);

            context.Publisher.Setup(publisher => publisher.Publish<IDomainEvent>(domainEvent, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            if (operation == "read")
            {
                context.ReadFailure = failure;
            }
            else if (operation == "update")
            {
                context.UpdateFailure = failure;
            }
            else
            {
                context.Transaction.Setup(transaction => transaction.Commit()).Throws(failure);
            }

            // Act
            Func<Task> act = () => context.Execute();

            // Assert
            (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(failure);

            context.Transaction.Verify(transaction => transaction.Commit(), operation == "commit" ? Times.Once() : Times.Never());

            context.Publisher.Verify(publisher => publisher.Publish<IDomainEvent>(domainEvent, It.IsAny<CancellationToken>()), operation == "read" ? Times.Never() : Times.Once());

            context.Publisher.VerifyNoOtherCalls();

            context.ConnectionDisposed.Should().BeTrue();

            context.TransactionDisposed.Should().BeTrue();
        }

        private void AssertCommittedAndDisposed()
        {
            context.Transaction.Verify(transaction => transaction.Commit(), Times.Once);

            context.Transactions.Should().OnlyContain(transaction => ReferenceEquals(transaction, context.Transaction.Object));

            context.Publisher.VerifyNoOtherCalls();

            context.ConnectionDisposed.Should().BeTrue();

            context.TransactionDisposed.Should().BeTrue();
        }
    }
}
