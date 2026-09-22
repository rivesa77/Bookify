namespace Bookify.Api.Tests.Support
{
    using System.Security.Claims;
    using System.Text.Encodings.Web;
    using Bookify.Api.Controllers.Constants;
    using Bookify.Api.Controllers.Users;
    using Bookify.Application.Abstractions.Data;
    using Bookify.Infrastructure;
    using MediatR;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.AspNetCore.TestHost;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Migrations;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Moq;

    internal sealed class ApiFactory : WebApplicationFactory<UsersController>
    {
        internal const string Scheme = "ApiTests";

        internal Mock<ISender> Sender { get; } = new(MockBehavior.Strict);

        internal Mock<IMigrator> Migrator { get; } = new(MockBehavior.Strict);

        internal SeedTestContext? Seed { get; private set; }

        private readonly bool development;

        private ServiceProvider? efServices;

        internal ApiFactory(bool development = false)
        {
            this.development = development;
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseEnvironment(development ? "Development" : "Testing");

            // Program reads the connection string before ConfigureTestServices runs.
            builder.ConfigureHostConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DataBase"] = "Host=127.0.0.1;Port=1;Database=unused;Username=test;Password=test;Timeout=1",

                ["Authentication:Audience"] = "api-tests",

                ["Authentication:Issuer"] = "https://identity.example/realms/tests",

                ["Authentication:MetadataUrl"] = "https://identity.example/realms/tests/.well-known/openid-configuration",

                ["Keycloak:BaseUrl"] = "https://identity.example",

                ["Keycloak:AdminUrl"] = "https://identity.example/admin/realms/tests/",

                ["Keycloak:TokenUrl"] = "https://identity.example/realms/tests/protocol/openid-connect/token"
            }));

            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(development ? "Development" : "Testing");

            builder.ConfigureTestServices(services =>
            {
                if (development)
                {
                    ConfigureDevelopmentServices(services);
                }

                services.RemoveAll<ISender>();

                services.AddSingleton(Sender.Object);

                services.RemoveAll<IClaimsTransformation>();

                services.AddSingleton<IClaimsTransformation, NoopClaimsTransformation>();

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = Scheme;
                    options.DefaultChallengeScheme = Scheme;
                    options.DefaultForbidScheme = Scheme;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(Scheme, _ => { });

                services.AddAuthorization(options => options.AddPolicy(
                    PermissionsConstants.UsersRead,
                    policy => policy.RequireClaim("permission", PermissionsConstants.UsersRead)));
            });
        }

        internal HttpClient CreateApiClient(bool authenticated = false)
        {
            HttpClient client = CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),

                AllowAutoRedirect = false
            });

            if (authenticated)
            {
                client.DefaultRequestHeaders.Add("X-Test-User", ApiTestData.UserId.ToString());
            }

            return client;
        }

        private void ConfigureDevelopmentServices(IServiceCollection services)
        {
            Seed = new SeedTestContext { HasApartments = true };

            Migrator.Setup(m => m.Migrate(null));

            ServiceCollection efRegistrations = new();

            efRegistrations.AddEntityFrameworkNpgsql();

            efRegistrations.AddSingleton(Migrator.Object);

            efServices = efRegistrations.BuildServiceProvider();

            DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql("Host=127.0.0.1;Port=1;Database=unused;Username=test;Password=test;Timeout=1")
                .UseInternalServiceProvider(efServices)
                .Options;

            services.RemoveAll<ApplicationDbContext>();

            services.AddScoped(_ => new ApplicationDbContext(options, Mock.Of<IPublisher>()));

            services.RemoveAll<ISqlConnectionFactory>();

            services.AddSingleton(Seed.Factory.Object);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                Seed?.Dispose();

                efServices?.Dispose();
            }
        }

        private sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
        {
            public TestAuthenticationHandler(
                IOptionsMonitor<AuthenticationSchemeOptions> options,
                ILoggerFactory logger,
                UrlEncoder encoder) : base(
                    options,
                    logger,
                    encoder)
            {
            }

            protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            {
                if (!Request.Headers.TryGetValue("X-Test-User", out Microsoft.Extensions.Primitives.StringValues userId))
                {
                    return Task.FromResult(AuthenticateResult.NoResult());
                }

                List<Claim> claims = [new Claim(ClaimTypes.NameIdentifier, userId.ToString())];

                if (Request.Headers.TryGetValue("X-Test-Role", out Microsoft.Extensions.Primitives.StringValues role))
                {
                    claims.Add(new Claim(ClaimTypes.Role, role.ToString()));
                }

                if (Request.Headers.TryGetValue("X-Test-Permission", out Microsoft.Extensions.Primitives.StringValues permission))
                {
                    claims.Add(new Claim("permission", permission.ToString()));
                }

                ClaimsPrincipal principal = new(new ClaimsIdentity(claims, ApiFactory.Scheme));

                return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, ApiFactory.Scheme)));
            }
        }
    }
}
