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
                .Where(user => user.Identity == identifyId)
                .Select(user => new UserRolesResponse
                {
                    Id = user.Id,
                    Roles = user.Roles.ToList(),
                }).FirstAsync();

            return userRolesResponse;
        }
    }
}