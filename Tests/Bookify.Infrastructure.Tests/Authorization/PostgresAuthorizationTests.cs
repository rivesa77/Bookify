namespace Bookify.Infrastructure.Tests.Authorization
{
    using System.IdentityModel.Tokens.Jwt;
    using System.Security.Claims;
    using Bookify.Domain.Apartments;
    using Bookify.Domain.Bookings;
    using Bookify.Domain.Users;
    using Bookify.Infrastructure.Authorization;
    using Bookify.Infrastructure.Tests.Support;
    using FluentAssertions;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using AuthorizationService = Bookify.Infrastructure.Authorization.AuthorizationService;

    [TestClass]
    [TestCategory("PostgreSQL")]
    public sealed class PostgresAuthorizationTests : PostgresTestBase
    {
        private const string ReadPermission = "users:read";

        private const string OtherPermission = "bookings:delete";

        [TestMethod]
        public async Task AuthorizationService_Should_ReturnLocalUserRolesAndPermissions()
        {
            // Arrange
            (User user, Apartment apartment, Booking booking) = await SeedBooking();

            AuthorizationService service = new(Context);

            // Act
            UserRolesResponse roles = await service.GetRolesForUserAsync(user.IdentityId);

            HashSet<string> permissions = await service.GetPermissionsForUserAsync(user.IdentityId);

            // Assert
            roles.Id.Should().Be(user.Id);

            roles.Roles.Should().ContainSingle().Which.Id.Should().Be(Role.Registered.Id);

            permissions.Should().BeEquivalentTo(new[] { ReadPermission });
        }

        [TestMethod]
        public async Task AuthorizationService_MissingUser_Should_ReportCurrentFirstAsyncFailure()
        {
            // Arrange
            AuthorizationService service = new(Context);

            // Act
            Func<Task> getRoles = () => service.GetRolesForUserAsync("missing");

            Func<Task> getPermissions = () => service.GetPermissionsForUserAsync("missing");

            // Assert
            await getRoles.Should().ThrowAsync<InvalidOperationException>();

            await getPermissions.Should().ThrowAsync<InvalidOperationException>();
        }

        [TestMethod]
        public async Task ClaimsTransformation_Should_AddLocalIdAndRolesOnlyOnce()
        {
            // Arrange
            (User user, Apartment apartment, Booking booking) = await SeedBooking();

            using ServiceProvider provider = CreateProvider();

            ClaimsPrincipal principal = Principal();

            CustomClaimsTransformation transformation = new(provider);

            // Act
            ClaimsPrincipal first = await transformation.TransformAsync(principal);

            ClaimsPrincipal second = await transformation.TransformAsync(first);

            // Assert
            first.Should().BeSameAs(principal);

            second.Should().BeSameAs(principal);

            principal.FindAll(JwtRegisteredClaimNames.Sub).Should().ContainSingle().Which.Value.Should().Be(user.Id.ToString());

            principal.FindAll(ClaimTypes.Role).Should().ContainSingle().Which.Value.Should().Be(Role.Registered.Name);

            principal.FindFirstValue(ClaimTypes.NameIdentifier).Should().Be(user.IdentityId);
        }

        [TestMethod]
        [DataRow(ReadPermission, true)]
        [DataRow(OtherPermission, false)]
        public async Task Handler_Should_RequireTheRequestedPermission(string requested, bool allowed)
        {
            // Arrange
            await SeedBooking();

            using ServiceProvider provider = CreateProvider();

            PermissionRequirement requirement = new(requested);

            AuthorizationHandlerContext context = AuthorizationContext(requirement);

            PermissionAuthorizationHandler handler = new(provider);

            // Act
            await handler.HandleAsync(context);

            // Assert
            context.HasSucceeded.Should().Be(allowed);
        }

        [TestMethod]
        public async Task Handler_EmptyPermissions_Should_NotSucceed()
        {
            // Arrange
            await SeedBooking();

            await Context.Set<RolePermission>().ExecuteDeleteAsync();

            using ServiceProvider provider = CreateProvider();

            PermissionAuthorizationHandler handler = new(provider);

            AuthorizationHandlerContext context = AuthorizationContext(new PermissionRequirement(ReadPermission));

            // Act
            await handler.HandleAsync(context);

            // Assert
            context.HasSucceeded.Should().BeFalse();
        }

        [TestMethod]
        public async Task GetPermissions_Should_CombineAllRoles()
        {
            // Arrange
            (User user, Apartment apartment, Booking booking) = await SeedBooking();

            User stored = await Context.Set<User>().Include(u => u.Roles).SingleAsync(u => u.Id == user.Id);

            Role extra = new(20, "BookingAdmin")
            {
                Permissions = [new Permission(20, OtherPermission)],

                Users = [stored]
            };

            Context.Add(extra);

            await Context.SaveChangesAsync();

            Context.ChangeTracker.Clear();

            AuthorizationService service = new(Context);

            // Act
            HashSet<string> permissions = await service.GetPermissionsForUserAsync(user.IdentityId);

            // Assert
            permissions.Should().BeEquivalentTo(new[] { ReadPermission, OtherPermission });
        }

        private ServiceProvider CreateProvider()
        {
            ServiceCollection services = new();

            services.AddScoped(_ => InfrastructureTestData.CreateContext(connectionString: ConnectionString));

            services.AddScoped<AuthorizationService>();

            return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        }

        private static ClaimsPrincipal Principal() => new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, InfrastructureTestData.IdentityId)],
            "test"));

        private static AuthorizationHandlerContext AuthorizationContext(PermissionRequirement requirement) => new(
            [requirement],
            Principal(),
            null);
    }
}
