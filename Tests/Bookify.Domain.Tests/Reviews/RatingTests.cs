namespace Bookify.Domain.Tests.Reviews
{
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Reviews;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Domain")]
    public sealed class RatingTests
    {
        [TestMethod]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        [DataRow(5)]
        public void Create_ValidRating_Should_PreserveValue(int value)
        {
            // Arrange
            int expected = value;

            // Act
            Result<Rating> result = Rating.Create(value);

            // Assert
            result.IsSuccess.Should().BeTrue();

            result.Value.Value.Should().Be(expected);

            result.Value.Should().Be(Rating.Create(expected).Value);
        }

        [TestMethod]
        [DataRow(int.MinValue)]
        [DataRow(-1)]
        [DataRow(0)]
        [DataRow(6)]
        [DataRow(int.MaxValue)]
        public void Create_InvalidRating_Should_ReturnError(int value)
        {
            // Arrange
            Error expected = Rating.Invalid;

            // Act
            Result<Rating> result = Rating.Create(value);

            // Assert
            result.IsFailure.Should().BeTrue();

            result.Error.Should().Be(expected);
        }
    }
}
