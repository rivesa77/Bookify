namespace Bookify.Infrastructure.Tests.Outbox
{
    using Bookify.Infrastructure.Outbox;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Infrastructure")]
    public sealed class OutboxMessageResponseTests
    {
        private const string Content = "{\"event\":\"created\"}";

        private const string OtherContent = "{\"event\":\"updated\"}";

        private readonly Guid messageId = Guid.NewGuid();

        [TestMethod]
        public void Constructor_Should_PreserveDatabaseProjection()
        {
            // Arrange

            Guid id = messageId;

            // Act

            OutboxMessageResponse response = new(id, Content);

            // Assert

            response.Id.Should().Be(id);

            response.Content.Should().Be(Content);
        }

        [TestMethod]
        [DataRow(true, true, true)]
        [DataRow(false, true, false)]
        [DataRow(true, false, false)]
        public void Equality_Should_CompareIdAndContent(
            bool sameId,
            bool sameContent,
            bool expected)
        {
            // Arrange

            OutboxMessageResponse response = new(messageId, Content);

            OutboxMessageResponse other = new(
                sameId ? messageId : Guid.NewGuid(),
                sameContent ? Content : OtherContent);

            // Act

            bool equal = response.Equals(other);

            // Assert

            equal.Should().Be(expected);
        }
    }
}
