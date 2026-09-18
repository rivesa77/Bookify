namespace Bookify.Infrastructure.Tests.Authentication
{
    using System.IdentityModel.Tokens.Jwt;
    using System.Security.Claims;
    using Bookify.Infrastructure.Authentication;
    using Bookify.Infrastructure.Authentication.Extensions;
    using Bookify.Infrastructure.Tests.Support;
    using FluentAssertions;
    using Microsoft.AspNetCore.Http;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Infrastructure")]
    public sealed class ClaimsTests
    {
        private static readonly Guid UserId = Guid.Parse("10000000-0000-0000-0000-000000000001");

        private readonly HttpContextAccessor accessor = new();

        [TestMethod]
        public void Extensions_Should_DistinguishExternalIdentityAndLocalUser()
        {
            // Arrange
            ClaimsPrincipal principal = Principal(UserId.ToString());

            // Act
            string identityId = principal.GetIdentityId();

            Guid userId = principal.GetUserId();

            // Assert
            identityId.Should().Be(InfrastructureTestData.IdentityId);

            userId.Should().Be(UserId);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("invalid-guid")]
        public void GetUserId_MissingOrInvalidClaim_Should_Throw(string? subject)
        {
            // Arrange
            ClaimsPrincipal principal = Principal(subject);

            // Act
            Func<Guid> act = () => principal.GetUserId();

            // Assert
            act.Should().Throw<ApplicationException>().WithMessage("User identifier is unavailable");
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void GetIdentityId_MissingPrincipalOrClaim_Should_Throw(bool nullPrincipal)
        {
            // Arrange
            ClaimsPrincipal? principal = nullPrincipal ? null : new ClaimsPrincipal();

            // Act
            Func<string> act = () => principal.GetIdentityId();

            // Assert
            act.Should().Throw<ApplicationException>().WithMessage("User identity is unavailable");
        }

        [TestMethod]
        public void GetUserId_NullPrincipal_Should_Throw()
        {
            // Arrange
            ClaimsPrincipal? principal = null;

            // Act
            Func<Guid> act = () => principal.GetUserId();

            // Assert
            act.Should().Throw<ApplicationException>();
        }

        [TestMethod]
        public void UserContext_Should_ReadCurrentHttpContext()
        {
            // Arrange
            accessor.HttpContext = new DefaultHttpContext { User = Principal(UserId.ToString()) };

            UserContext context = new(accessor);

            // Act
            Guid userId = context.UserId;

            string identityId = context.IdentityId;

            // Assert
            userId.Should().Be(UserId);

            identityId.Should().Be(InfrastructureTestData.IdentityId);
        }

        [TestMethod]
        public void UserContext_WithoutHttpContext_Should_ThrowForBothIdentifiers()
        {
            // Arrange
            accessor.HttpContext = null;

            UserContext context = new(accessor);

            // Act
            Func<Guid> getUser = () => context.UserId;

            Func<string> getIdentity = () => context.IdentityId;

            // Assert
            getUser.Should().Throw<ApplicationException>().WithMessage("User context is unavailable");

            getIdentity.Should().Throw<ApplicationException>().WithMessage("User context is unavailable");
        }

        private static ClaimsPrincipal Principal(string? subject)
        {
            ClaimsIdentity identity = new("test");

            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, InfrastructureTestData.IdentityId));

            if (subject is not null)
            {
                identity.AddClaim(new Claim(JwtRegisteredClaimNames.Sub, subject));
            }

            return new ClaimsPrincipal(identity);
        }
    }
}
