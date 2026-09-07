namespace Bookify.Infrastructure
{
    using Bookify.Domain.Abstractions;
    using Microsoft.EntityFrameworkCore;

    public sealed class ApplicationDbContext : DbContext, IUnitOfWork
    {
        public ApplicationDbContext(DbContextOptions options)
            : base(options)
        {
        }
    }
}