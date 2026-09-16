namespace Bookify.Api.Controllers.Users
{
    public sealed record CreateUserRequest(
        string FirstName,
        string LastName,
        string Email,
        string Password);
}