namespace Bookify.Domain.Apartments
{
    using System;

    public interface IApartmentRepository
    {
        Task<Apartment?> GetByIdAsync(Guid Id, CancellationToken cancellationToken = default);
    }
}