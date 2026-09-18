namespace Bookify.Infrastructure.Tests.Data
{
    using System.Data;
    using Bookify.Domain.Users;
    using Bookify.Infrastructure.Clock;
    using Bookify.Infrastructure.Data;
    using Bookify.Infrastructure.Email;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Npgsql;

    [TestClass]
    [TestCategory("Infrastructure")]
    public sealed class AdapterTests
    {
        private readonly DateOnlyTypeHandler handler = new();

        [TestMethod]
        public void DateOnlyHandler_Should_RemoveTimeOnRead()
        {
            // Arrange
            DateTime value = new(2026, 9, 17, 19, 30, 45);

            // Act
            DateOnly result = handler.Parse(value);

            // Assert
            result.Should().Be(DateOnly.FromDateTime(value));
        }

        [TestMethod]
        public void DateOnlyHandler_Should_SetParameterTypeAndValue()
        {
            // Arrange
            NpgsqlParameter parameter = new();

            DateOnly value = new(2026, 9, 17);

            // Act
            handler.SetValue(parameter, value);

            // Assert
            parameter.DbType.Should().Be(DbType.Date);

            parameter.Value.Should().Be(value);
        }

        [TestMethod]
        public void DateOnlyHandler_UnsupportedInput_Should_Throw()
        {
            // Arrange
            const string value = "2026-09-17";

            // Act
            Func<DateOnly> act = () => handler.Parse(value);

            // Assert
            act.Should().Throw<InvalidCastException>();
        }

        [TestMethod]
        public void Clock_Should_ReturnCurrentUtcWithinObservedInterval()
        {
            // Arrange
            DateTimeProvider provider = new();

            DateTime before = DateTime.UtcNow;

            // Act
            DateTime result = provider.UtcNow;

            DateTime after = DateTime.UtcNow;

            // Assert
            result.Kind.Should().Be(DateTimeKind.Utc);

            result.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        }

        [TestMethod]
        public async Task EmailService_Should_CompleteWithoutExternalDelivery()
        {
            // Arrange
            EmailService service = new();

            // Act
            Func<Task> act = () => service.SendAsync(
                new Email("test@example.com"),
                "Subject",
                "Body");

            // Assert
            await act.Should().NotThrowAsync();
        }
    }
}