namespace Bookify.Domain.Tests.Apartments
{
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Apartments;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Domain")]
    public sealed class NameTests
    {
        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow(" ")]
        [DataRow("\t\r\n")]
        public void Create_EmptyText_Should_ReturnEmptyError(string? value)
        {
            // Arrange
            Error expected = NameErrors.Empty;

            // Act
            Result<Name> result = Name.Create(value!);

            // Assert
            result.IsFailure.Should().BeTrue();

            result.Error.Should().Be(expected);
        }

        [TestMethod]
        [DataRow(1, true)]
        [DataRow(199, true)]
        [DataRow(200, true)]
        [DataRow(201, false)]
        public void Create_Should_EnforceMaximumLength(int length, bool valid)
        {
            // Arrange
            string value = new('a', length);

            // Act
            Result<Name> result = Name.Create(value);

            // Assert
            Name.ExactLength.Should().Be(200);

            result.IsSuccess.Should().Be(valid);

            if (valid)
            {
                result.Value.Value.Should().Be(value);
            }
            else
            {
                result.Error.Should().Be(NameErrors.InvalidLength);
            }
        }

        [TestMethod]
        public void Create_Should_PreserveWhitespaceAndUseValueEquality()
        {
            // Arrange
            const string value = " Apartment ";

            // Act
            Name first = Name.Create(value).Value;

            Name second = Name.Create(value).Value;

            // Assert
            first.Value.Should().Be(value);

            first.Should().Be(second);

            first.Should().NotBe(Name.Create(value.Trim()).Value);
        }
    }
}
