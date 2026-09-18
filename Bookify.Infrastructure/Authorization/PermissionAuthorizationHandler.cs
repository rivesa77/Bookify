namespace Bookify.Infrastructure.Authorization
{
    using Bookify.Infrastructure.Authentication.Extensions;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.Extensions.DependencyInjection;

    internal sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
    {
        private readonly IServiceProvider serviceProvider;

        public PermissionAuthorizationHandler(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            if (context.User.Identity is not { IsAuthenticated: true })
            {
                return;
            }

            using IServiceScope scope = serviceProvider.CreateScope();

            AuthorizationService authorizationService = scope.ServiceProvider.GetRequiredService<AuthorizationService>();

            string identityId = context.User.GetIdentityId();

            HashSet<string> permissions = await authorizationService.GetPermissionsForUserAsync(identityId);

            if (permissions.Contains(requirement.Permission))
            {
                context.Succeed(requirement);
            }
        }
    }
}