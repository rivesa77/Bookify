namespace Bookify.Infrastructure.Authorization
{
    using System.IdentityModel.Tokens.Jwt;
    using System.Security.Claims;
    using Bookify.Domain.Users;
    using Bookify.Infrastructure.Authentication.Extensions;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.Extensions.DependencyInjection;

    internal sealed class CustomClaimsTransformation : IClaimsTransformation
    {
        private readonly IServiceProvider serviceProvider;

        public CustomClaimsTransformation(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            if (principal.HasClaim(claim => claim.Type == ClaimTypes.Role) &&
                principal.HasClaim(claim => claim.Type == JwtRegisteredClaimNames.Sub))
            {
                return principal;
            }

            using IServiceScope scope = serviceProvider.CreateScope();

            AuthorizationService authorizationService = scope.ServiceProvider.GetRequiredService<AuthorizationService>();

            string identityId = principal.GetIdentityId();

            UserRolesResponse userRoles = await authorizationService.GetRolesForUserAsync(identityId);

            ClaimsIdentity claimsIdentity = new();

            Claim claim = new(JwtRegisteredClaimNames.Sub, userRoles.Id.ToString());

            claimsIdentity.AddClaim(claim);

            foreach (Role role in userRoles.Roles)
            {
                Claim claimRole = new(ClaimTypes.Role, role.Name);

                claimsIdentity.AddClaim(claimRole);
            }

            principal.AddIdentity(claimsIdentity);

            return principal;
        }
    }
}