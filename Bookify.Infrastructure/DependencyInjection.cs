namespace Bookify.Infrastructure
{
    using Bookify.Application.Abstractions.DateTimeProvider;
    using Bookify.Application.Abstractions.Email;
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Apartments;
    using Bookify.Domain.Bookings;
    using Bookify.Domain.Users;
    using Bookify.Infrastructure.Clock;
    using Bookify.Infrastructure.Email;
    using Bookify.Infrastructure.Repositories;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services
                .AddTransient<IDateTimeProvider, DateTimeProvider>()
                .AddTransient<IEmailService, EmailService>();

            services
                .AddScoped<IApartmentRepository, ApartmentRepository>()
                .AddScoped<IBookingRepository, BookingRepository>()
                .AddScoped<IUserRepository, UserRepository>();

            services
                .AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ApplicationDbContext>());

            string connectionString = configuration.GetConnectionString("DataBase") ??
                throw new ArgumentNullException(nameof(configuration));

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                // UseSnakeCaseNamingConvention automatically map C# class and property names to snake_case in the database.
                options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
            });

            return services;
        }
    }
}