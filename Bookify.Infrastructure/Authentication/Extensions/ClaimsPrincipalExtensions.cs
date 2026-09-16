namespace Bookify.Infrastructure.Authentication.Extensions
{
    using System;
    using System.Security.Claims;

    internal static class ClaimsPrincipalExtensions
    {
        public static string GetIdentityId(this ClaimsPrincipal? principal)
        {
            return principal?.FindFirstValue(ClaimTypes.NameIdentifier) ??
                   throw new ApplicationException("User identity is unavailable");
        }
    }
}