namespace Bookify.Infrastructure.Tests
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
    using Bookify.Infrastructure.Outbox;
    using Bookify.Infrastructure.Tests.Support;
    using FluentAssertions;
    using MediatR;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Options;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Quartz;
    using AuthenticationOptions = Bookify.Infrastructure.Authentication.AuthenticationOptions;
    using IAuthenticationService = Bookify.Application.Abstractions.Authentication.IAuthenticationService;

    [TestClass]
    [TestCategory("Infrastructure")]
    public sealed class DependencyInjectionTests
    {
        private readonly ServiceCollection registrations = new();

        private readonly KeycloakOptions keycloak = InfrastructureTestData.Keycloak();

        private const string Audience = "bookify-api";

        private const string Issuer = "https://identity.example/realms/bookify";

        private const int OutboxIntervalInSeconds = 10;

        private const int OutboxBatchSize = 20;

        [TestMethod]
        public void AddInfrastructure_Should_ResolveServicesAndRespectScopedLifetimes()
        {
            // Arrange
            using ServiceProvider provider = CreateProvider();

            using IServiceScope first = provider.CreateScope();

            using IServiceScope second = provider.CreateScope();

            // Act
            ApplicationDbContext context = first.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            IUnitOfWork unitOfWork = first.ServiceProvider.GetRequiredService<IUnitOfWork>();

            // Assert
            unitOfWork.Should().BeSameAs(context);

            second.ServiceProvider.GetRequiredService<ApplicationDbContext>().Should().NotBeSameAs(context);

            first.ServiceProvider.GetRequiredService<IApartmentRepository>().Should().BeSameAs(first.ServiceProvider.GetRequiredService<IApartmentRepository>());

            first.ServiceProvider.GetRequiredService<IBookingRepository>().Should().NotBeNull();

            first.ServiceProvider.GetRequiredService<IReviewRepository>().Should().NotBeNull();

            first.ServiceProvider.GetRequiredService<IUserRepository>().Should().NotBeNull();

            first.ServiceProvider.GetRequiredService<IUserContext>().Should().NotBeNull();

            first.ServiceProvider.GetRequiredService<IAuthenticationService>().Should().NotBeNull();

            first.ServiceProvider.GetRequiredService<IJwtService>().Should().NotBeNull();

            first.ServiceProvider.GetRequiredService<IEmailService>().Should().NotBeNull();

            first.ServiceProvider.GetRequiredService<IDateTimeProvider>().Should().NotBeNull();

            first.ServiceProvider.GetRequiredService<IClaimsTransformation>().Should().BeOfType<CustomClaimsTransformation>();

            first.ServiceProvider.GetServices<IAuthorizationHandler>().Should().Contain(h => h is PermissionAuthorizationHandler);

            first.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>().Should().BeOfType<PermissionAuthorizationPolicyProvider>();

            first.ServiceProvider.GetRequiredService<ISqlConnectionFactory>().Should().BeSameAs(second.ServiceProvider.GetRequiredService<ISqlConnectionFactory>());

            context.Database.GetConnectionString().Should().Be(InfrastructureTestData.ConnectionString);
        }

        [TestMethod]
        public void AddInfrastructure_Should_BindAuthenticationAndKeycloakOptions()
        {
            // Arrange
            using ServiceProvider provider = CreateProvider();

            // Act
            JwtBearerOptions bearer = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);

            KeycloakOptions actual = provider.GetRequiredService<IOptions<KeycloakOptions>>().Value;

            // Assert
            bearer.Audience.Should().Be(Audience);

            bearer.MetadataAddress.Should().Be(Issuer + "/.well-known/openid-configuration");

            bearer.RequireHttpsMetadata.Should().BeTrue();

            bearer.TokenValidationParameters.ValidIssuer.Should().Be(Issuer);

            actual.Should().BeEquivalentTo(keycloak);
        }

        [TestMethod]
        public void AddInfrastructure_Should_RegisterOutboxWorkerAndBindOptions()
        {
            // Arrange
            using ServiceProvider provider = CreateProvider();

            // Act
            OutboxOptions options = provider.GetRequiredService<IOptions<OutboxOptions>>().Value;

            // Assert
            options.IntervalInSeconds.Should().Be(OutboxIntervalInSeconds);

            options.BatchSize.Should().Be(OutboxBatchSize);

            registrations.Should().ContainSingle(service =>
                service.ServiceType == typeof(IHostedService) &&
                service.ImplementationType != null &&
                service.ImplementationType.FullName == "Quartz.QuartzHostedService");

            provider.GetServices<IConfigureOptions<QuartzOptions>>()
                .Should().ContainSingle(setup => setup is ProcessOutboxMessagesJobSetup);
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void JwtSetup_Should_ApplyBothConfigureOverloads(bool named)
        {
            // Arrange
            AuthenticationOptions source = new()
            {
                Audience = Audience,
                MetadataUrl = Issuer,
                Issuer = Issuer,
                RequireHttpsMetadata = false
            };

            JwtBearerOptionsSetup setup = new(Options.Create(source));

            JwtBearerOptions target = new();

            // Act
            if (named)
            {
                setup.Configure("test", target);
            }
            else
            {
                setup.Configure(target);
            }

            // Assert
            target.Audience.Should().Be(Audience);

            target.MetadataAddress.Should().Be(Issuer);

            target.RequireHttpsMetadata.Should().BeFalse();

            target.TokenValidationParameters.ValidIssuer.Should().Be(Issuer);
        }

        [TestMethod]
        public void AddInfrastructure_MissingConnectionString_Should_FailImmediately()
        {
            // Arrange
            IConfiguration configuration = new ConfigurationBuilder().Build();

            // Act
            Action act = () => registrations.AddInfrastructure(configuration);

            // Assert
            act.Should().Throw<ArgumentNullException>();
        }

        private ServiceProvider CreateProvider()
        {
            Dictionary<string, string?> values = new()
            {
                ["ConnectionStrings:DataBase"] = InfrastructureTestData.ConnectionString,
                ["Authentication:Audience"] = Audience,
                ["Authentication:Issuer"] = Issuer,
                ["Authentication:MetadataUrl"] = Issuer + "/.well-known/openid-configuration",
                ["Authentication:RequireHttpsMetadata"] = "true",
                ["Keycloak:BaseUrl"] = "https://identity.example",
                ["Keycloak:AdminUrl"] = keycloak.AdminUrl,
                ["Keycloak:TokenUrl"] = keycloak.TokenUrl,
                ["Keycloak:AdminClientId"] = keycloak.AdminClientId,
                ["Keycloak:AdminClientSecret"] = keycloak.AdminClientSecret,
                ["Keycloak:AuthClientId"] = keycloak.AuthClientId,
                ["Keycloak:AuthClientSecret"] = keycloak.AuthClientSecret,
                ["Outbox:IntervalInSeconds"] = OutboxIntervalInSeconds.ToString(),
                ["Outbox:BatchSize"] = OutboxBatchSize.ToString()
            };

            registrations.AddLogging();

            registrations.AddSingleton(Mock.Of<IPublisher>());

            registrations.AddSingleton(Mock.Of<IHostApplicationLifetime>());

            registrations.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(values).Build());

            return registrations.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,

                ValidateOnBuild = true
            });
        }
    }
}
