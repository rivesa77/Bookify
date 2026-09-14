namespace Bookify.Infrastructure.Repositories
{
    using Bookify.Domain.Reviews;

    internal sealed class ReviewRepository : Repository<Review>, IReviewRepository
    {
        public ReviewRepository(ApplicationDbContext applicationDbContext)
            : base(applicationDbContext)
        {
        }
    }
}