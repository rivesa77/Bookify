namespace Bookify.Api.Controllers.Users
{
    using Bookify.Api.Controllers.Constants;
    using Bookify.Application.Users.CreateUser;
    using Bookify.Application.Users.GetLoggedInUser;
    using Bookify.Application.Users.LogInUser;
    using Bookify.Domain.Abstractions;
    using MediatR;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    [Authorize]
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly ISender sender;

        public UsersController(ISender sender)
        {
            this.sender = sender;
        }

        [Authorize(Roles = RolesConstants.Registered)]
        [HttpGet("LogInUser")]
        public async Task<IActionResult> LogInUser(CancellationToken cancellationToken)
        {
            GetLoggedInUserQuery query = new();

            Result<UserResponse> result = await sender.Send(query, cancellationToken);

            return Ok(result.Value);
        }

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register(
            CreateUserRequest request,
            CancellationToken cancellationToken)
        {
            CreateUserCommand command = new(
                request.FirstName,
                request.LastName,
                request.Email,
                request.Password);

            Result<Guid> result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
            {
                return BadRequest(result.Error);
            }

            return Ok(result.Value);
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login(
            LoginUserRequest request,
            CancellationToken cancellationToken)
        {
            LogInUserCommand command = new(
                request.Email,
                request.Password);

            Result<AccessTokenResponse> result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
            {
                return Unauthorized(result.Error);
            }

            return Ok(result.Value);
        }
    }
}