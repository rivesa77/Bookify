namespace Bookify.Infrastructure.Authorization
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.Extensions.Options;

    internal sealed class PermissionAuthorizationPolicyProvider : DefaultAuthorizationPolicyProvider
    {
        private readonly AuthorizationOptions authorizationOptions;

        public PermissionAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
            : base(options)
        {
            authorizationOptions = options.Value;
        }

        public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            AuthorizationPolicy? authorizationPolicy = await base.GetPolicyAsync(policyName);

            if (authorizationPolicy is not null)
            {
                return authorizationPolicy;
            }

            PermissionRequirement permissionRequirement = new(policyName);

            AuthorizationPolicy permission = new AuthorizationPolicyBuilder()
                .AddRequirements(permissionRequirement)
                .Build();

            authorizationOptions.AddPolicy(policyName, permission);

            return permission;
        }
    }
}