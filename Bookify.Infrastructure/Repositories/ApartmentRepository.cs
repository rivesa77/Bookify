namespace Bookify.Infrastructure.Repositories
{
    using Bookify.Domain.Apartments;

    internal sealed class ApartmentRepository : Repository<Apartment>, IApartmentRepository
    {
        public ApartmentRepository(ApplicationDbContext applicationDbContext) : base(applicationDbContext)
        {
        }
    }
}