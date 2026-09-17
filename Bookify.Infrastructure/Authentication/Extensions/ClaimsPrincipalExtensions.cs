namespace Bookify.Infrastructure.Authentication.Extensions
{
    using System;
    using System.IdentityModel.Tokens.Jwt;
    using System.Security.Claims;

    internal static class ClaimsPrincipalExtensions
    {
        public static string GetIdentityId(this ClaimsPrincipal? principal)
        {
            return principal?.FindFirstValue(ClaimTypes.NameIdentifier) ??
                   throw new ApplicationException("User identity is unavailable");
        }

        public static Guid GetUserId(this ClaimsPrincipal? principal)
        {
            string userId = principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)!;

            return Guid.TryParse(userId, out Guid parseUserId) ?
                parseUserId :
                throw new ApplicationException("User identifier is unavailable"); ;
        }
    }
}