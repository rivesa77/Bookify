namespace Bookify.Application.Tests.Users
{
    using Bookify.Application.Exceptions;
    using Bookify.Application.Tests.Support;
    using Bookify.Application.Users.CreateUser;
    using Bookify.Application.Users.LogInUser;
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Users;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("Application")]
    public sealed class UserCommandTests
    {
        private const string FirstName = "Ana";

        private const string LastName = "Garcia";

        private const string Email = "ana@example.com";

        private const string Password = "secret123";

        private const string IdentityId = "external-identity";

        private const string AccessToken = "access-token";

        private static CreateUserCommand ValidCreateUserCommand() => new CreateUserCommand(
            FirstName,
            LastName,
            Email,
            Password);

        private static LogInUserCommand ValidLogInUserCommand() => new LogInUserCommand(Email, Password);

        private readonly Bookify.Application.Tests.Support.ApplicationTestContext context = new();

        private readonly CancellationTokenSource cancellation = new();

        [TestCleanup]
        public void Cleanup()
        {
            context.Dispose();

            cancellation.Dispose();
        }

        [TestMethod]
        public async Task CreateUser_Should_RegisterIdentityBeforePersistingUser()
        {
            // Arrange

            CreateUserCommand command = ValidCreateUserCommand();

            User? registered = null;

            User? saved = null;

            MockSequence sequence = new MockSequence();

            context.Authentication.InSequence(sequence)
                .Setup(a => a.RegisterAsync(
                It.IsAny<User>(),
                command.Password,
                cancellation.Token))
                .Callback<User, string, CancellationToken>((
                user,
                _,
                _) => registered = user)
                .ReturnsAsync(IdentityId);

            context.Users.InSequence(sequence).Setup(r => r.Add(It.Is<User>(u => u.IdentityId == IdentityId)))
                .Callback<User>(user => saved = user);

            context.UnitOfWork.InSequence(sequence).Setup(u => u.SaveChangesAsync(cancellation.Token)).ReturnsAsync(1);

            // Act

            Result<Guid> result = await context.Sender.Send(command, cancellation.Token);

            // Assert

            result.IsSuccess.Should().BeTrue();

            saved.Should().BeSameAs(registered);

            result.Value.Should().Be(saved!.Id).And.NotBeEmpty();

            saved.FirstName.Value.Should().Be(command.FirtsName);

            saved.LastName.Value.Should().Be(command.LastName);

            saved.Email.Value.Should().Be(command.Email);

            saved.Roles.Should().ContainSingle().Which.Should().Be(Role.Registered);

            context.Authentication.VerifyAll();

            context.Users.VerifyAll();

            context.UnitOfWork.VerifyAll();

            context.Authentication.VerifyNoOtherCalls();

            context.Users.VerifyNoOtherCalls();

            context.UnitOfWork.VerifyNoOtherCalls();
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task CreateUser_DependencyFailure_Should_Propagate(bool failOnSave)
        {
            // Arrange

            CreateUserCommand command = ValidCreateUserCommand();

            InvalidOperationException failure = new InvalidOperationException("Service unavailable");

            Moq.Language.Flow.ISetup<Abstractions.Authentication.IAuthenticationService, Task<string>> registration = context.Authentication.Setup(a => a.RegisterAsync(
                It.IsAny<User>(),
                command.Password,
                It.IsAny<CancellationToken>()));

            if (failOnSave)
            {
                registration.ReturnsAsync("identity");

                context.Users.Setup(r => r.Add(It.IsAny<User>()));

                context.UnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(failure);
            }
            else
            {
                registration.ThrowsAsync(failure);
            }

            // Act

            Func<Task> act = () => context.Sender.Send(command);

            // Assert

            (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(failure);

            if (!failOnSave)
            {
                context.Users.VerifyNoOtherCalls();

                context.UnitOfWork.VerifyNoOtherCalls();
            }
        }

        [TestMethod]
        public async Task Login_Should_ReturnTokenAndForwardCancellation()
        {
            // Arrange

            LogInUserCommand command = ValidLogInUserCommand();

            context.Jwt.Setup(j => j.GetAccessTokenAsync(
                command.Email,
                command.Password,
                cancellation.Token))
                .ReturnsAsync(Result.Success(AccessToken));

            // Act

            Result<AccessTokenResponse> result = await context.Sender.Send(command, cancellation.Token);

            // Assert

            result.Value.Should().Be(new AccessTokenResponse(AccessToken));

            context.Jwt.Verify(j => j.GetAccessTokenAsync(
                command.Email,
                command.Password,
                cancellation.Token), Times.Once);

            context.Jwt.VerifyNoOtherCalls();

            context.UnitOfWork.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task Login_ServiceFailure_Should_ReturnInvalidCredentials()
        {
            // Arrange

            context.Jwt.Setup(j => j.GetAccessTokenAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Failure<string>(new Error("Provider.Error", "Provider details")));

            // Act

            Result<AccessTokenResponse> result = await context.Sender.Send(ValidLogInUserCommand());

            // Assert

            result.IsFailure.Should().BeTrue();

            result.Error.Should().Be(UserErrors.InvalidCredentials);
        }

        [TestMethod]
        public async Task Login_Cancellation_Should_Propagate()
        {
            // Arrange

            context.Jwt.Setup(j => j.GetAccessTokenAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                cancellation.Token))
                .ThrowsAsync(new OperationCanceledException(cancellation.Token));

            // Act

            Func<Task> act = () => context.Sender.Send(ValidLogInUserCommand(), cancellation.Token);

            // Assert

            (await act.Should().ThrowAsync<OperationCanceledException>()).Which.CancellationToken.Should().Be(cancellation.Token);
        }

        [TestMethod]
        public async Task InvalidUserCommands_Should_StopBeforeDependencies()
        {
            // Arrange

            // Act

            Func<Task> create = () => context.Sender.Send(new CreateUserCommand(
                "",
                "",
                "invalid",
                ""));

            Func<Task> login = () => context.Sender.Send(new LogInUserCommand("invalid", ""));

            // Assert

            await create.Should().ThrowAsync<ValidationException>();

            await login.Should().ThrowAsync<ValidationException>();

            context.Authentication.VerifyNoOtherCalls();

            context.Jwt.VerifyNoOtherCalls();

            context.Users.VerifyNoOtherCalls();

            context.UnitOfWork.VerifyNoOtherCalls();
        }
    }
}
