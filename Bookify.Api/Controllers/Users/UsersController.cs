namespace Bookify.Api.Controllers.Users
{
    using Bookify.Application.Users.CreateUser;
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

            return Ok(result);
        }
    }
}