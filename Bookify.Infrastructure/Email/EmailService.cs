namespace Bookify.Infrastructure.Email
{
    using Bookify.Application.Abstractions.Email;
    using Bookify.Domain.Users;

    internal sealed class EmailService : IEmailService
    {
        public Task SendAsync(Email recipient, string subject, string body)
        {
            return Task.CompletedTask;
        }
    }
}