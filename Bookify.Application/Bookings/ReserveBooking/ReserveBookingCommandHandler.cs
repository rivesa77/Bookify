namespace Bookify.Application.Bookings.ReserveBooking
{
    using Bookify.Application.Abstractions.DateTimeProvider;
    using Bookify.Application.Abstractions.Messaging;
    using Bookify.Application.Exceptions;
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Apartments;
    using Bookify.Domain.Bookings;
    using Bookify.Domain.Users;

    internal sealed class ReserveBookingCommandHandler : ICommandHandler<ReserveBookingCommand, Guid>
    {
        private readonly IUserRepository userRepository;
        private readonly IApartmentRepository apartmentRepository;
        private readonly IBookingRepository bookingRepository;
        private readonly IUnitOfWork unitOfWork;
        private readonly PricingServices pricingServices;
        private readonly IDateTimeProvider dateTimeProvider;

        public ReserveBookingCommandHandler(
            IUserRepository userRepository,
            IApartmentRepository apartmentRepository,
            IBookingRepository bookingRepository,
            IUnitOfWork unitOfWork,
            PricingServices pricingServices,
            IDateTimeProvider dateTimeProvider)
        {
            this.userRepository = userRepository;
            this.apartmentRepository = apartmentRepository;
            this.bookingRepository = bookingRepository;
            this.unitOfWork = unitOfWork;
            this.pricingServices = pricingServices;
            this.dateTimeProvider = dateTimeProvider;
        }

        public async Task<Result<Guid>> Handle(ReserveBookingCommand request, CancellationToken cancellationToken)
        {
            User? user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);

            if (user is null)
            {
                return Result.Failure<Guid>(UserErrors.NotFound);
            }

            Apartment? apartment = await apartmentRepository.GetByIdAsync(request.ApartmentId, cancellationToken);

            if (apartment is null)
            {
                return Result.Failure<Guid>(ApartmentErrors.NotFound);
            }

            DateRange period = DateRange.Create(request.StartDate, request.EndDate);

            bool isOverlapping = await bookingRepository.IsOverlappingAsync(apartment, period, cancellationToken);

            if (isOverlapping)
            {
                return Result.Failure<Guid>(BookingErrors.Overlap);
            }

            try
            {
                Booking booking = Booking.Reserve(
                    apartment,
                    user.Id,
                    period,
                    dateTimeProvider.UtcNow,
                    pricingServices);

                bookingRepository.Add(booking);

                await unitOfWork.SaveChangesAsync(cancellationToken);

                return booking.Id;
            }
            catch (ConcurrencyException)
            {
                return Result.Failure<Guid>(BookingErrors.Overlap);
            }
        }
    }
}