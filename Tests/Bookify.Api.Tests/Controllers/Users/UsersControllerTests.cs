namespace Bookify.Api.Tests.Controllers.Users
{
    using Bookify.Api.Controllers.Users;
    using Bookify.Api.Tests.Support;
    using Bookify.Application.Users.CreateUser;
    using Bookify.Application.Users.GetLoggedInUser;
    using Bookify.Application.Users.LogInUser;
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Users;
    using FluentAssertions;
    using MediatR;
    using Microsoft.AspNetCore.Mvc;
    using Moq;

    [TestClass]
    [TestCategory("Controller")]
    public sealed class UsersControllerTests
    {
        private readonly Mock<ISender> sender = new(MockBehavior.Strict);

        private readonly CancellationTokenSource cancellation = new();

        private UsersController controller = null!;

        [TestInitialize]
        public void Initialize() => controller = new UsersController(sender.Object);

        [TestCleanup]
        public void Cleanup() => cancellation.Dispose();

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task Register_Should_MapFieldsAndReturnSuccessOrError(bool success)
        {
            // Arrange
            CreateUserRequest request = ApiTestData.UserRequest();

            CreateUserCommand command = new(
                request.FirstName,
                request.LastName,
                request.Email,
                request.Password);

            Result<Guid> result = success ? Result.Success(ApiTestData.Id) : Result.Failure<Guid>(ApiTestData.Failure);

            sender.Setup(s => s.Send(command, cancellation.Token)).ReturnsAsync(result);

            // Act
            IActionResult response = await controller.Register(request, cancellation.Token);

            // Assert
            ObjectResult actual = response.Should().BeAssignableTo<ObjectResult>().Subject;

            actual.StatusCode.Should().Be(success ? 200 : 400);

            actual.Value.Should().Be(success ? (object)ApiTestData.Id : ApiTestData.Failure);

            sender.Verify(s => s.Send(command, cancellation.Token), Times.Once);

            sender.VerifyNoOtherCalls();
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task Login_Should_ReturnTokenOrUnauthorized(bool success)
        {
            // Arrange
            CreateUserRequest user = ApiTestData.UserRequest();

            LoginUserRequest request = new(user.Email, user.Password);

            LogInUserCommand command = new(request.Email, request.Password);

            AccessTokenResponse token = new("access-token");

            Result<AccessTokenResponse> result = success ? Result.Success(token) : Result.Failure<AccessTokenResponse>(UserErrors.InvalidCredentials);

            sender.Setup(s => s.Send(command, cancellation.Token)).ReturnsAsync(result);

            // Act
            IActionResult response = await controller.Login(request, cancellation.Token);

            // Assert
            ObjectResult actual = response.Should().BeAssignableTo<ObjectResult>().Subject;

            actual.StatusCode.Should().Be(success ? 200 : 401);

            actual.Value.Should().Be(success ? (object)token : UserErrors.InvalidCredentials);

            sender.Verify(s => s.Send(command, cancellation.Token), Times.Once);

            sender.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task LogInUser_Should_ReturnProfileAndForwardToken()
        {
            // Arrange
            UserResponse user = new() { Id = ApiTestData.UserId };

            GetLoggedInUserQuery query = new();

            sender.Setup(s => s.Send(query, cancellation.Token)).ReturnsAsync(Result.Success(user));

            // Act
            IActionResult result = await controller.LogInUser(cancellation.Token);

            // Assert
            result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(user);

            sender.Verify(s => s.Send(query, cancellation.Token), Times.Once);

            sender.VerifyNoOtherCalls();
        }
    }
}
