namespace Bookify.Application.Abstractions.Email
{
    using Bookify.Domain.Users;

    public interface IEmailService
    {
        Task SendAsync(
            Email recipient,
            string subject,
            string body);
    }
}