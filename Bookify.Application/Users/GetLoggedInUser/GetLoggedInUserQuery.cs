namespace Bookify.Application.Users.GetLoggedInUser
{
    using Bookify.Application.Abstractions.Messaging;

    public sealed record GetLoggedInUserQuery() : IQuery<UserResponse>
    {
    }
}