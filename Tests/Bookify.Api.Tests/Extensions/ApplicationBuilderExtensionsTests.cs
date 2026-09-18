namespace Bookify.Api.Tests.Extensions
{
    using Bookify.Api.Extensions;
    using Bookify.Infrastructure;
    using FluentAssertions;
    using MediatR;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Migrations;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;

    [TestClass]
    [TestCategory("Extensions")]
    public sealed class ApplicationBuilderExtensionsTests
    {
        private readonly Mock<IMigrator> migrator = new(MockBehavior.Strict);

        private ServiceProvider efServices = null!;

        private ServiceProvider appServices = null!;

        private ApplicationDbContext? resolvedContext;

        [TestInitialize]
        public void Initialize()
        {
            ServiceCollection efRegistrations = new();

            efRegistrations.AddEntityFrameworkNpgsql();

            efRegistrations.AddSingleton(migrator.Object);

            efServices = efRegistrations.BuildServiceProvider();

            DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql("Host=127.0.0.1;Port=1;Database=unused;Username=test;Password=test;Timeout=1")
                .UseInternalServiceProvider(efServices)
                .Options;

            ServiceCollection registrations = new();

            registrations.AddScoped(_ => resolvedContext = new ApplicationDbContext(options, Mock.Of<IPublisher>()));

            appServices = registrations.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        }

        [TestCleanup]
        public void Cleanup()
        {
            appServices.Dispose();

            efServices.Dispose();
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ApplyMigration_Should_InvokeMigratorAndDisposeScopeEvenOnFailure(bool fails)
        {
            // Arrange
            InvalidOperationException error = new("Migration failed");

            if (fails)
            {
                migrator.Setup(m => m.Migrate(null)).Throws(error);
            }
            else
            {
                migrator.Setup(m => m.Migrate(null));
            }

            ApplicationBuilder app = new(appServices);

            // Act
            Action act = () => app.ApplyMigration();

            // Assert
            if (fails)
            {
                act.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(error);
            }
            else
            {
                act.Should().NotThrow();
            }

            migrator.Verify(m => m.Migrate(null), Times.Once);

            migrator.VerifyNoOtherCalls();

            resolvedContext.Should().NotBeNull();

            Action useDisposedContext = () => resolvedContext!.ChangeTracker.Clear();

            useDisposedContext.Should().Throw<ObjectDisposedException>();
        }
    }
}
