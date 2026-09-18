namespace Bookify.Infrastructure.Authorization
{
    using Bookify.Domain.Users;
    using Microsoft.EntityFrameworkCore;

    internal sealed class AuthorizationService
    {
        private readonly ApplicationDbContext applicationDbContext;

        public AuthorizationService(ApplicationDbContext applicationDbContext)
        {
            this.applicationDbContext = applicationDbContext;
        }

        public async Task<UserRolesResponse> GetRolesForUserAsync(string identifyId)
        {
            UserRolesResponse userRolesResponse = await applicationDbContext.Set<User>()
                .Where(user => user.IdentityId == identifyId)
                .Select(user => new UserRolesResponse
                {
                    Id = user.Id,
                    Roles = user.Roles.ToList(),
                }).FirstAsync();

            return userRolesResponse;
        }

        public async Task<HashSet<string>> GetPermissionsForUserAsync(string identityId)
        {
            List<string> permissions = await applicationDbContext.Set<User>()
                .Where(user => user.IdentityId == identityId)
                .SelectMany(user => user.Roles)
                .SelectMany(role => role.Permissions)
                .Select(permission => permission.Name)
                .Distinct()
                .ToListAsync();

            return [.. permissions];
        }
    }
}