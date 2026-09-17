namespace Bookify.Application.Tests.Behaviors
{
    using Bookify.Application.Abstractions.Behaviors;
    using Bookify.Application.Exceptions;
    using Bookify.Application.Users.CreateUser;
    using Bookify.Domain.Abstractions;
    using FluentAssertions;
    using FluentValidation;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using ValidationException = Bookify.Application.Exceptions.ValidationException;

    [TestClass]
    [TestCategory("Application")]
    public sealed class PipelineBehaviorTests
    {
        private const string StartMessage = "Executing command CreateUserCommand";

        private const string HandlerFailureMessage = "Handler failed";

        private readonly Mock<ILogger<CreateUserCommand>> logger = new();

        private readonly CancellationTokenSource cancellation = new();

        private LoggingBehavior<CreateUserCommand, Result<Guid>> loggingBehavior = null!;

        [TestInitialize]
        public void Initialize()
        {
            loggingBehavior = new LoggingBehavior<CreateUserCommand, Result<Guid>>(logger.Object);
        }

        [TestCleanup]
        public void Cleanup()
        {
            cancellation.Dispose();
        }

        private static ValidationBehavior<CreateUserCommand, Result<Guid>> CreateValidationBehavior(
            params IValidator<CreateUserCommand>[] validators) => new(validators);

        private static CreateUserCommand Command() => new(
            "Ana",
            "Garcia",
            "ana@example.com",
            "12345");

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task Validation_Should_CallNextOnceWithToken_WhenNoErrors(bool withValidator)
        {
            // Arrange
            IValidator<CreateUserCommand>[] validators = withValidator ? new IValidator<CreateUserCommand>[] { new CreateUserCommandHandlerValidator() } : [];

            ValidationBehavior<CreateUserCommand, Result<Guid>> behavior = CreateValidationBehavior(validators);

            Result<Guid> response = Result.Success(Guid.NewGuid());

            int calls = 0;

            CancellationToken received = default;

            // Act

            Result<Guid> result = await behavior.Handle(
                Command(),
                token =>
                {
                    calls++;

                    received = token;

                    return Task.FromResult(response);
                },
                cancellation.Token);

            // Assert

            result.Should().BeSameAs(response);

            calls.Should().Be(1);

            received.Should().Be(cancellation.Token);
        }

        [TestMethod]
        public async Task Validation_Should_AggregateAllValidatorsAndStopNext()
        {
            // Arrange
            InlineValidator<CreateUserCommand> first = new();

            first.RuleFor(c => c.Email).Must(_ => false).WithMessage("Email rejected");

            InlineValidator<CreateUserCommand> second = new();

            second.RuleFor(c => c.Password).Must(_ => false).WithMessage("Password rejected");

            ValidationBehavior<CreateUserCommand, Result<Guid>> behavior = CreateValidationBehavior(first, second);

            int calls = 0;

            // Act

            Func<Task> act = () => behavior.Handle(
                Command(),
                _ =>
                {
                    calls++;

                    return Task.FromResult(Result.Success(Guid.NewGuid()));
                },
                default);

            // Assert

            ValidationException error = (await act.Should().ThrowAsync<ValidationException>()).Which;

            error.Errors.Should().BeEquivalentTo(new[]
            {
            new ValidationError("Email", "Email rejected"),

            new ValidationError("Password", "Password rejected")
        });

            calls.Should().Be(0);
        }

        [TestMethod]
        public async Task Validation_Should_PropagateNextException()
        {
            // Arrange
            ValidationBehavior<CreateUserCommand, Result<Guid>> behavior = CreateValidationBehavior();

            InvalidOperationException failure = new(HandlerFailureMessage);

            // Act

            Func<Task> act = () => behavior.Handle(
                Command(),
                _ => throw failure,
                default);

            // Assert

            (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(failure);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task Logging_Should_ReturnSameResultAndLogStartAndCompletion(bool businessFailure)
        {
            // Arrange
            Result<Guid> expected = businessFailure ? Result.Failure<Guid>(new Error("Business.Error", "Rejected")) : Result.Success(Guid.NewGuid());

            int calls = 0;

            CancellationToken received = default;

            // Act

            Result<Guid> result = await loggingBehavior.Handle(
                Command(),
                token =>
                {
                    calls++;

                    received = token;

                    return Task.FromResult(expected);
                },
                cancellation.Token);

            // Assert

            result.Should().BeSameAs(expected);

            calls.Should().Be(1);

            received.Should().Be(cancellation.Token);

            LogMessages(logger).Should().Equal(StartMessage, "Command CreateUserCommand processed successfully");

            logger.Invocations.Where(i => i.Method.Name == "Log").Should().OnlyContain(i => (LogLevel)i.Arguments[0] == LogLevel.Information);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task Logging_Should_LogFailureAndPreserveException(bool cancelled)
        {
            // Arrange
            Exception failure = cancelled ? new OperationCanceledException() : new InvalidOperationException(HandlerFailureMessage);

            // Act

            Func<Task> act = () => loggingBehavior.Handle(
                Command(),
                _ => Task.FromException<Result<Guid>>(failure),
                default);

            // Assert

            (await act.Should().ThrowAsync<Exception>()).Which.Should().BeSameAs(failure);

            LogMessages(logger).Should().Equal(StartMessage, "Command CreateUserCommand processed failed");
        }

        [TestMethod]
        public void Exceptions_Should_PreserveDiagnosticInformation()
        {
            // Arrange
            InvalidOperationException inner = new("Storage conflict");

            ValidationError[] errors = new[] { new ValidationError("Email", "Invalid address") };

            // Act

            ConcurrencyException concurrency = new("Could not save", inner);

            ValidationException validation = new(errors);

            // Assert

            concurrency.Message.Should().Be("Could not save");

            concurrency.InnerException.Should().BeSameAs(inner);

            validation.Errors.Should().BeSameAs(errors);

            validation.Errors.Should().ContainSingle().Which.Should().Be(new ValidationError("Email", "Invalid address"));
        }

        private static IEnumerable<string?> LogMessages(Mock<ILogger<CreateUserCommand>> logger) =>
            logger.Invocations
            .Where(i => i.Method.Name == "Log")
            .Select(i => i.Arguments[2].ToString());
    }
}