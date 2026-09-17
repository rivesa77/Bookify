namespace Bookify.Domain.Tests.Commons
{
    using Bookify.Domain.Apartments;
    using Bookify.Domain.Reviews;
    using Bookify.Domain.Users;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Domain")]
    public sealed class TextValueObjectTests
    {
        private const string Value = "original text";

        private const string DifferentValue = "different text";

        [TestMethod]
        [DataRow(nameof(Description))]
        [DataRow(nameof(Comment))]
        [DataRow(nameof(FirstName))]
        [DataRow(nameof(LastName))]
        [DataRow(nameof(Email))]
        public void Constructor_Should_PreserveTextAndCompareByValue(string type)
        {
            // Arrange
            object expected = Create(type, Value);

            // Act
            object same = Create(type, Value);

            object different = Create(type, DifferentValue);

            // Assert
            ReadValue(same).Should().Be(Value);

            same.Should().Be(expected);

            different.Should().NotBe(expected);
        }

        private static object Create(string type, string value) => type switch
        {
            nameof(Description) => new Description(value),

            nameof(Comment) => new Comment(value),

            nameof(FirstName) => new FirstName(value),

            nameof(LastName) => new LastName(value),

            nameof(Email) => new Email(value),

            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        private static string ReadValue(object value) => value switch
        {
            Description description => description.Value,

            Comment comment => comment.Value,

            FirstName firstName => firstName.Value,

            LastName lastName => lastName.Value,

            Email email => email.Value,

            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };
    }
}
