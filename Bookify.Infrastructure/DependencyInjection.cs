namespace Bookify.Infrastructure
{
    using Bookify.Application.Abstractions.DateTimeProvider;
    using Bookify.Application.Abstractions.Email;
    using Bookify.Infrastructure.Clock;
    using Bookify.Infrastructure.Email;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services
                .AddTransient<IDateTimeProvider, DateTimeProvider>()
                .AddTransient<IEmailService, EmailService>();

            return services;
        }
    }
}