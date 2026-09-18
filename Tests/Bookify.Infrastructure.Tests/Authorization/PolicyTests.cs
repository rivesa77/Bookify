namespace Bookify.Infrastructure.Tests.Authorization
{
    using System.IdentityModel.Tokens.Jwt;
    using System.Security.Claims;
    using Bookify.Infrastructure.Authorization;
    using FluentAssertions;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Infrastructure")]
    public sealed class PolicyTests
    {
        private const string Permission = "users:read";

        private readonly AuthorizationOptions options = new();

        private readonly ServiceProvider services = new ServiceCollection().BuildServiceProvider();

        [TestCleanup]
        public void Cleanup() => services.Dispose();

        [TestMethod]
        public async Task GetPolicy_Should_CreateAndReusePermissionRequirement()
        {
            // Arrange
            PermissionAuthorizationPolicyProvider provider = Provider();

            // Act
            AuthorizationPolicy? first = await provider.GetPolicyAsync(Permission);

            AuthorizationPolicy? second = await provider.GetPolicyAsync(Permission);

            // Assert
            first.Should().NotBeNull();

            first!.Requirements.Should().ContainSingle().Which.Should().BeOfType<PermissionRequirement>()
                .Which.Permission.Should().Be(Permission);

            second.Should().BeSameAs(first);
        }

        [TestMethod]
        public async Task GetPolicy_Should_PreserveConfiguredPolicy()
        {
            // Arrange
            AuthorizationPolicy expected = new AuthorizationPolicyBuilder().RequireRole("Admin").Build();

            options.AddPolicy(Permission, expected);

            PermissionAuthorizationPolicyProvider provider = Provider();

            // Act
            AuthorizationPolicy? result = await provider.GetPolicyAsync(Permission);

            // Assert
            result.Should().BeSameAs(expected);
        }

        [TestMethod]
        public async Task Provider_Should_PreserveDefaultAndFallbackPolicies()
        {
            // Arrange
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

            PermissionAuthorizationPolicyProvider provider = Provider();

            // Act
            AuthorizationPolicy defaultPolicy = await provider.GetDefaultPolicyAsync();

            AuthorizationPolicy? fallback = await provider.GetFallbackPolicyAsync();

            // Assert
            defaultPolicy.Should().BeSameAs(options.DefaultPolicy);

            fallback.Should().BeSameAs(options.FallbackPolicy);
        }

        [TestMethod]
        public void Attribute_Should_UsePermissionAsPolicyName()
        {
            // Arrange
            string expected = Permission;

            // Act
            HasPermissionAttribute attribute = new(expected);

            // Assert
            attribute.Policy.Should().Be(expected);
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task Handler_AnonymousPrincipal_Should_NotRequireServices(bool hasIdentity)
        {
            // Arrange
            ClaimsPrincipal principal = hasIdentity ? new ClaimsPrincipal(new ClaimsIdentity()) : new ClaimsPrincipal();

            PermissionRequirement requirement = new(Permission);

            AuthorizationHandlerContext context = new(
                [requirement],
                principal,
                null);

            PermissionAuthorizationHandler handler = new(services);

            // Act
            await handler.HandleAsync(context);

            // Assert
            context.HasSucceeded.Should().BeFalse();

            context.PendingRequirements.Should().Contain(requirement);
        }

        [TestMethod]
        public async Task ClaimsTransformation_AlreadyEnriched_Should_NotRequireServicesOrDuplicateClaims()
        {
            // Arrange
            ClaimsPrincipal principal = new(new ClaimsIdentity(
                [new Claim(ClaimTypes.Role, "Registered"), new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString())],
                "test"));

            CustomClaimsTransformation transformation = new(services);

            // Act
            ClaimsPrincipal result = await transformation.TransformAsync(principal);

            // Assert
            result.Should().BeSameAs(principal);

            result.Claims.Should().HaveCount(2);
        }

        private PermissionAuthorizationPolicyProvider Provider() => new(Options.Create(options));
    }
}
