namespace Bookify.Infrastructure.Repositories
{
    using Bookify.Domain.Users;

    internal sealed class UserRepository : Repository<User>, IUserRepository
    {
        public UserRepository(ApplicationDbContext applicationDbContext)
            : base(applicationDbContext)
        {
        }

        public override void Add(User user)
        {
            // Indicates that the role information is already in the database and does not need to be saved.
            foreach (Role role in user.Roles)
            {
                applicationDbContext.Attach(role);
            }

            base.Add(user);
        }
    }
}