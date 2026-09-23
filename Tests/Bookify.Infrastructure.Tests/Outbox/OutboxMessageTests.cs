namespace Bookify.Infrastructure.Tests.Outbox
{
    using Bookify.Domain.Users.Events;
    using Bookify.Infrastructure.Outbox;
    using Bookify.Infrastructure.Tests.Support;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Infrastructure")]
    public sealed class OutboxMessageTests
    {
        private const string EventType = nameof(UserCreatedDomainEvent);

        private const string Content = "{\"UserId\":\"00000000-0000-0000-0000-000000000001\"}";

        private readonly Guid messageId = Guid.NewGuid();

        [TestMethod]
        public void Constructor_Should_PreserveMessageData()
        {
            // Arrange

            DateTime occurredOnUtc = InfrastructureTestData.UtcNow;

            // Act

            OutboxMessage message = CreateMessage(occurredOnUtc);

            // Assert

            message.Id.Should().Be(messageId);

            message.OccurredOnUtc.Should().Be(occurredOnUtc);

            message.OccurredOnUtc.Kind.Should().Be(DateTimeKind.Utc);

            message.Type.Should().Be(EventType);

            message.Content.Should().Be(Content);
        }

        [TestMethod]
        public void Constructor_Should_CreatePendingMessageWithoutError()
        {
            // Arrange

            DateTime occurredOnUtc = InfrastructureTestData.UtcNow;

            // Act

            OutboxMessage message = CreateMessage(occurredOnUtc);

            // Assert

            message.ProcessedOnUtc.Should().BeNull();

            message.Error.Should().BeNull();
        }

        private OutboxMessage CreateMessage(DateTime occurredOnUtc) => new(
            messageId,
            occurredOnUtc,
            EventType,
            Content);
    }
}
