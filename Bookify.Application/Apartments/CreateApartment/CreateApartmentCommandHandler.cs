namespace Bookify.Application.Apartments.CreateApartment
{
    using Bookify.Application.Abstractions.Messaging;
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Apartments;
    using Bookify.Domain.Commons;

    internal sealed class CreateApartmentCommandHandler : ICommandHandler<CreateApartmentCommand, Guid>
    {
        private readonly IApartmentRepository apartmentRepository;
        private readonly IUnitOfWork unitOfWork;

        public CreateApartmentCommandHandler(IApartmentRepository apartmentRepository, IUnitOfWork unitOfWork)
        {
            this.apartmentRepository = apartmentRepository;
            this.unitOfWork = unitOfWork;
        }

        public async Task<Result<Guid>> Handle(CreateApartmentCommand request, CancellationToken cancellationToken)
        {
            Result<Name> name = Name.Create(request.Name);

            if (name.IsFailure)
            {
                return Result.Failure<Guid>(name.Error);
            }

            Currency currency = Currency.FromCode(request.Currency);

            Apartment apartment = new(
                Guid.NewGuid(),
                name.Value,
                new Description(request.Description),
                new Address(request.Country, request.State, request.ZipCode, request.City, request.Street),
                new Money(request.PriceAmount, currency),
                new Money(request.CleaningFeeAmount, currency),
                [.. request.Amenities]);

            apartmentRepository.Add(apartment);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return apartment.Id;
        }
    }
}