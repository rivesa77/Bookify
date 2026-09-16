namespace Bookify.Application.Users.LogInUser
{
    using Bookify.Application.Abstractions.Messaging;

    public sealed record LogInUserCommand(
        string Email,
        string Password) : ICommand<AccessTokenResponse>;
}