namespace Bookify.Infrastructure
{
    using Bookify.Application.Abstractions.Authentication;
    using Bookify.Application.Abstractions.Data;
    using Bookify.Application.Abstractions.DateTimeProvider;
    using Bookify.Application.Abstractions.Email;
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Apartments;
    using Bookify.Domain.Bookings;
    using Bookify.Domain.Reviews;
    using Bookify.Domain.Users;
    using Bookify.Infrastructure.Authentication;
    using Bookify.Infrastructure.Authorization;
    using Bookify.Infrastructure.Clock;
    using Bookify.Infrastructure.Data;
    using Bookify.Infrastructure.Email;
    using Bookify.Infrastructure.Repositories;
    using Dapper;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;
    using AuthenticationOptions = Authentication.AuthenticationOptions;
    using AuthenticationService = Bookify.Infrastructure.Authentication.AuthenticationService;
    using IAuthenticationService = Application.Abstractions.Authentication.IAuthenticationService;

    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services
                .AddTransient<IDateTimeProvider, DateTimeProvider>()
                .AddTransient<IEmailService, EmailService>();

            AddPersistence(services, configuration);

            AddAuthentication(services, configuration);

            AddAuthorization(services, configuration);

            AddHealthChecks(services, configuration);

            return services;
        }

        private static void AddAuthentication(IServiceCollection services, IConfiguration configuration)
        {
            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer();

            services
                .Configure<AuthenticationOptions>(configuration.GetSection("Authentication"))
                .Configure<KeycloakOptions>(configuration.GetSection("Keycloak"))
                .ConfigureOptions<JwtBearerOptionsSetup>();

            services
                .AddTransient<AdminAuthorizationDelegatingHandler>();

            services.AddHttpClient<IAuthenticationService, AuthenticationService>((serviceProvider, httpClient) =>
            {
                var keycloakOptions = serviceProvider.GetRequiredService<IOptions<KeycloakOptions>>().Value;

                httpClient.BaseAddress = new Uri(keycloakOptions.AdminUrl);
            })
                .AddHttpMessageHandler<AdminAuthorizationDelegatingHandler>();

            services.AddHttpClient<IJwtService, JwtService>((serviceProvider, httpClient) =>
            {
                var keycloakOptions = serviceProvider.GetRequiredService<IOptions<KeycloakOptions>>().Value;

                httpClient.BaseAddress = new Uri(keycloakOptions.TokenUrl);
            });

            services.AddScoped<IUserContext, UserContext>();
        }

        private static void AddAuthorization(IServiceCollection services, IConfiguration configuration)
        {
            services
                .AddScoped<AuthorizationService>();

            services
                .AddTransient<IClaimsTransformation, CustomClaimsTransformation>()
                .AddTransient<IAuthorizationHandler, PermissionAuthorizationHandler>()
                .AddTransient<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
        }

        private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
        {
            services
                .AddScoped<IApartmentRepository, ApartmentRepository>()
                .AddScoped<IBookingRepository, BookingRepository>()
                .AddScoped<IReviewRepository, ReviewRepository>()
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

            services
                .AddSingleton<ISqlConnectionFactory>(_ => new SqlConnectionFactory(connectionString));

            services
                .AddHttpContextAccessor();

            SqlMapper
                .AddTypeHandler(new DateOnlyTypeHandler());
        }

        private static void AddHealthChecks(IServiceCollection services, IConfiguration configuration)
        {
            services.AddHealthChecks()
                .AddNpgSql(configuration.GetConnectionString("Database")!)
                .AddUrlGroup(
                    new Uri(configuration["KeyCloak:BaseUrl"]!),
                    HttpMethod.Get,
                    "keycloak");
        }
    }
}