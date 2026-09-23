namespace Bookify.Infrastructure.Tests.Outbox
{
    using Bookify.Infrastructure.Outbox;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Infrastructure")]
    public sealed class OutboxOptionsTests
    {
        [TestMethod]
        public void Constructor_Should_DefaultToZeroWithoutConfiguration()
        {
            // Arrange

            const int expected = 0;

            // Act

            OutboxOptions options = new();

            // Assert

            options.IntervalInSeconds.Should().Be(expected);

            options.BatchSize.Should().Be(expected);
        }

        [TestMethod]
        [DataRow(1, 1)]
        [DataRow(10, 20)]
        public void Initialization_Should_PreserveConfiguredValues(int intervalInSeconds, int batchSize)
        {
            // Arrange

            int expectedInterval = intervalInSeconds;

            int expectedBatchSize = batchSize;

            // Act

            OutboxOptions options = new()
            {
                IntervalInSeconds = intervalInSeconds,

                BatchSize = batchSize
            };

            // Assert

            options.IntervalInSeconds.Should().Be(expectedInterval);

            options.BatchSize.Should().Be(expectedBatchSize);
        }
    }
}
