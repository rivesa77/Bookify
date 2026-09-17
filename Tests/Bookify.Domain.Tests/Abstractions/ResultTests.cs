namespace Bookify.Domain.Tests.Abstractions
{
    using Bookify.Domain.Abstractions;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Domain")]
    public sealed class ResultTests
    {
        private const string Value = "value";

        private static readonly Error Failure = new("Test.Failure", "Test failure");

        [TestMethod]
        public void Success_Should_HaveNoError()
        {
            // Arrange
            Error expected = Error.None;

            // Act
            Result result = Result.Success();

            // Assert
            result.IsSuccess.Should().BeTrue();

            result.IsFailure.Should().BeFalse();

            result.Error.Should().Be(expected);
        }

        [TestMethod]
        public void Failure_Should_PreserveError()
        {
            // Arrange
            Error expected = Failure;

            // Act
            Result result = Result.Failure(expected);

            // Assert
            result.IsSuccess.Should().BeFalse();

            result.IsFailure.Should().BeTrue();

            result.Error.Should().BeSameAs(expected);
        }

        [TestMethod]
        public void GenericSuccess_Should_ExposeValue()
        {
            // Arrange
            string expected = Value;

            // Act
            Result<string> result = Result.Success(expected);

            // Assert
            result.IsSuccess.Should().BeTrue();

            result.Error.Should().Be(Error.None);

            result.Value.Should().Be(expected);
        }

        [TestMethod]
        public void GenericFailure_Should_RejectValueAccess()
        {
            // Arrange
            Result<string> result = Result.Failure<string>(Failure);

            // Act
            Func<string> act = () => result.Value;

            // Assert
            result.IsFailure.Should().BeTrue();

            result.Error.Should().BeSameAs(Failure);

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("The value of a failure result can not be accessed.");
        }

        [TestMethod]
        [DataRow(true, true, true)]
        [DataRow(true, false, false)]
        [DataRow(false, true, false)]
        [DataRow(false, false, true)]
        public void Constructor_Should_EnforceSuccessAndErrorConsistency(
            bool success,
            bool noError,
            bool valid)
        {
            // Arrange
            Error error = noError ? Error.None : Failure;

            // Act
            Func<Result> act = () => new TestResult(success, error);

            // Assert
            if (valid)
            {
                Result result = act.Should().NotThrow().Subject;

                result.IsSuccess.Should().Be(success);

                result.Error.Should().Be(error);
            }
            else
            {
                act.Should().Throw<InvalidOperationException>();
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void Failure_WithNoError_Should_Throw(bool generic)
        {
            // Arrange
            Error error = Error.None;

            // Act
            Func<Result> act = () => generic ? Result.Failure<string>(error) : Result.Failure(error);

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }

        [TestMethod]
        [DataRow(null, false)]
        [DataRow("", true)]
        [DataRow(Value, true)]
        public void Create_AndImplicitConversion_Should_HandleNullableValues(string? value, bool success)
        {
            // Arrange
            Error expected = success ? Error.None : Error.NullValue;

            // Act
            Result<string> created = Result.Create<string>(value);

            Result<string> converted = value;

            // Assert
            created.IsSuccess.Should().Be(success);

            converted.IsSuccess.Should().Be(success);

            created.Error.Should().Be(expected);

            converted.Error.Should().Be(expected);

            if (success)
            {
                created.Value.Should().Be(value);

                converted.Value.Should().Be(value);
            }
        }

        [TestMethod]
        public void Create_WithDefaultValueType_Should_Succeed()
        {
            // Arrange
            int value = default;

            // Act
            Result<int> result = Result.Create(value);

            // Assert
            result.IsSuccess.Should().BeTrue();

            result.Value.Should().Be(0);
        }

        private sealed class TestResult : Result
        {
            internal TestResult(bool success, Error error) : base(success, error)
            {
            }
        }
    }
}
