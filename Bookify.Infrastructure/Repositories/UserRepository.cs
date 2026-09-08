namespace Bookify.Infrastructure.Repositories
{
    using Bookify.Domain.Users;

    internal sealed class UserRepository : Repository<User>, IUserRepository
    {
        public UserRepository(ApplicationDbContext applicationDbContext)
            : base(applicationDbContext)
        {
        }
    }
}