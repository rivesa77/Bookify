namespace Bookify.Infrastructure.Authorization
{
    using Microsoft.AspNetCore.Authorization;

    internal sealed class PermissionRequirement : IAuthorizationRequirement
    {
        public string Permission { get; set; }

        public PermissionRequirement(string permission)
        {
            Permission = permission;
        }
    }
}