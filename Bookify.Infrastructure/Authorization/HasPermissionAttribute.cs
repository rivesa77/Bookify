namespace Bookify.Infrastructure.Authorization
{
    using Microsoft.AspNetCore.Authorization;

    public sealed class HasPermissionAttribute : AuthorizeAttribute
    {
        public HasPermissionAttribute(string permission)
            : base(permission)
        {
        }
    }
}