namespace Bookify.Application.Users.CreateUser
{
    using Bookify.Application.Abstractions.Messaging;

    public sealed record CreateUserCommand(
        string FirtsName,
        string LastName,
        string Email,
        string Password) : ICommand<Guid>;
}