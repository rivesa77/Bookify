namespace Bookify.Application.Abstractions.Authentication
{
    using Bookify.Domain.Abstractions;

    public interface IJwtService
    {
        Task<Result<string>> GetAccessTokenAsync(
            string email,
            string password,
            CancellationToken cancellationToken);
    }
}