namespace Bookify.Application.Abstractions.Authentication
{
    using Bookify.Domain.Users;

    public interface IAuthenticationService
    {
        Task<string> RegisterAsync(
            User user,
            string password,
            CancellationToken cancellationToken);
    }
}