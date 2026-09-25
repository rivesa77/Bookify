namespace Bookify.Application.Apartments.UpdateApartment
{
    using Bookify.Application.Abstractions.Messaging;
    using Bookify.Application.Exceptions;
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Apartments;
    using Bookify.Domain.Commons;

    internal sealed class UpdateApartmentCommandHandler : ICommandHandler<UpdateApartmentCommand>
    {
        private readonly IApartmentRepository apartmentRepository;

        private readonly IUnitOfWork unitOfWork;

        public UpdateApartmentCommandHandler(IApartmentRepository apartmentRepository, IUnitOfWork unitOfWork)
        {
            this.apartmentRepository = apartmentRepository;

            this.unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(UpdateApartmentCommand request, CancellationToken cancellationToken)
        {
            Result<Name> name = Name.Create(request.Name);

            if (name.IsFailure)
            {
                return Result.Failure(name.Error);
            }

            Apartment? apartment = await apartmentRepository.GetByIdAsync(request.ApartmentId, cancellationToken);

            if (apartment is null)
            {
                return Result.Failure(ApartmentErrors.NotFound);
            }

            Currency currency = Currency.FromCode(request.Currency);

            apartment.Update(
                name.Value,
                new Description(request.Description),
                new Address(
                    request.Country,
                    request.State,
                    request.ZipCode,
                    request.City,
                    request.Street),
                new Money(request.PriceAmount, currency),
                new Money(request.CleaningFeeAmount, currency),
                request.Amenities);

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (ConcurrencyException)
            {
                return Result.Failure(ApartmentErrors.Conflict);
            }

            return Result.Success();
        }
    }
}
